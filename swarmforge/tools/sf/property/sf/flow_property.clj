(ns sf.flow-property
  (:require [clojure.string :as str]
            [clojure.test.check.generators :as gen]
            [clojure.test.check.properties :as prop]
            [speclj.core :refer [describe it should]]
            [sf.config :as config]
            [sf.flow :as flow]
            [sf.shell :as shell]
            [sf.property-support :refer [passes? role sentence]]))

(def ^:private catalogue ["specifier" "coder" "cleaner" "architect" "hardener" "qa"])

(def ^:private route
  "A route: a non-empty subsequence of the catalogue."
  (gen/fmap #(str/join "," (filter (set %) catalogue)) (gen/not-empty (gen/set (gen/elements catalogue)))))

(def ^:private needs (gen/elements ["none" "operator" "approval"]))

(def ^:private state
  (gen/let [r route
            stage (gen/elements (flow/route-list r))
            n needs]
    {:stage stage :route r :needs n}))

(describe "next-stage"
  (it "walks the route in order from its first role to done"
    (should (passes? (prop/for-all [r route]
                       (let [roles (flow/route-list r)
                             walk (take (inc (count roles))
                                        (iterate #(flow/next-stage {:stage % :route r} nil) (first roles)))]
                         (= (concat roles ["done"]) walk))))))

  (it "goes where --to says"
    (should (passes? (prop/for-all [s state to role]
                       (= to (flow/next-stage s to)))))))

(describe "refusals"
  (it "refuse a handoff when waiting or lacking specification approval"
    (should (passes? (prop/for-all [s state]
                       (= (or (flow/waiting? s) (= "specifier" (:stage s)))
                          (some? (flow/handoff-refusal s)))))))

  (it "allow specification handoff only at the approved role commit"
    (should (passes? (prop/for-all [n needs approved (gen/elements ["c1" "c2"]) head (gen/elements ["c1" "c2"])]
                       (= (and (= "none" n) (= approved head))
                          (nil? (flow/handoff-refusal {:stage "specifier" :needs n :record "approval"
                                                      :from "operator" :commit approved} head)))))))

  (it "approve only the reviewed role commit while waiting for approval"
    (should (passes? (prop/for-all [s state reviewed (gen/elements [nil "c1" "c2"]) head (gen/elements ["c1" "c2"])]
                       (= (and (= "approval" (:needs s)) (= reviewed head))
                          (nil? (flow/approval-refusal s reviewed head)))))))

  (it "keep any route that holds the stage"
    (should (passes? (prop/for-all [s state]
                       (nil? (flow/route-refusal s (:route s))))))))

(describe "records"
  (it "carry the stage, route and needs on unless their fields change them"
    (should (passes? (prop/for-all [s state from role fields (gen/map (gen/elements [:needs :route :text]) sentence)]
                       (= (merge s fields {:record "answer" :from from})
                          (flow/follow (assoc s :sha "abc" :commit "def") "answer" from fields))))))

  (it "hand off with a summary that is never blank, waiting on no one"
    (should (passes? (prop/for-all [s state from role summary (gen/one-of [(gen/return nil) (gen/return " ") sentence])]
                       (let [rec (flow/handoff s from (flow/next-stage s nil) "abc1234" summary)]
                         (and (not (str/blank? (:text rec)))
                              (not (flow/waiting? rec))
                              (= (:route s) (:route rec)))))))))

(describe "optional catalogue roles"
  (it "preserves order and audit flags while excluding only implicit optional roles"
    (should (passes? (prop/for-all [optional (gen/set (gen/elements (rest catalogue)))
                                   audited (gen/set (gen/elements catalogue))
                                   reverse-flags? gen/boolean]
                       (let [rows (mapv (fn [role]
                                          (let [flags (cond-> []
                                                        (optional role) (conj "optional")
                                                        (audited role) (conj "audit"))]
                                            (into [role "strong" "subagent"]
                                                  (if reverse-flags? (reverse flags) flags))))
                                        catalogue)
                             explicit (str/join "," catalogue)]
                         (with-redefs [config/rows #(when (= "roles.conf" %) rows)
                                       shell/env (fn [_ default] default)]
                           (and (= (str/join "," (remove optional catalogue)) (config/default-route))
                                (= explicit (config/resolve-route explicit))
                                (= (mapv #(boolean (audited %)) catalogue)
                                   (mapv config/role-audits? catalogue))))))))))

(def ^:private finish-facts
  "What sf finish knows of a task: [recorded? tip tip-merged?]."
  (gen/tuple gen/boolean (gen/elements [nil "t1"]) gen/boolean))

(describe "finish-action"
  (it "refuses only a task without a branch, and merges only a tip that HEAD has no trace of"
    (should (passes? (prop/for-all [[recorded? tip merged? :as facts] finish-facts]
                       (let [action (apply flow/finish-action facts)]
                         (and (= (nil? action) (nil? tip))
                              (= (= :merge action) (and (some? tip) (not recorded?) (not merged?)))))))))

  (it "completes a merge already in HEAD only when HEAD contains the tip, recording it when HEAD has no record"
    (should (passes? (prop/for-all [[recorded? tip merged? :as facts] finish-facts]
                       (let [action (apply flow/finish-action facts)]
                         (and (= (#{:keep :record} action) (when (and tip merged?) action))
                              (= (= :unmerged action) (and (some? tip) recorded? (not merged?)))
                              (= (= :record action) (and (some? tip) merged? (not recorded?))))))))))

(def ^:private review-catalogue ["specifier" "coder" "cleaner" "architect" "pr-reviewer" "qa"])

(def ^:private review-route
  "A route of at least two roles from a catalogue with a reviewer before qa."
  (gen/fmap #(str/join "," (filter (set %) review-catalogue))
            (gen/such-that #(<= 2 (count %)) (gen/set (gen/elements review-catalogue)))))

(defn- handoff-record [from to] {:record "handoff" :from from :stage to})

(def ^:private routed-records
  "A route and handoff records between any two of its roles."
  (gen/let [r review-route
            pairs (gen/vector (gen/tuple (gen/elements (flow/route-list r)) (gen/elements (flow/route-list r))) 0 6)]
    [r (mapv #(apply handoff-record %) pairs)]))

(defn- earlier-on?
  "True when a and b are on route and a comes before b."
  [route a b]
  (let [roles (flow/route-list route)]
    (< -1 (.indexOf ^java.util.List roles a) (.indexOf ^java.util.List roles b))))

(defn- walk
  "Hands the task along route from its first role, one target per step, with
  the stages in flagged able to send back, keeping each handoff that
  to-refusal allows. Returns the handoff records."
  [route flagged targets]
  (loop [stage (first (flow/route-list route)) records [] [to & more] targets]
    (if (or (nil? to) (= "done" stage))
      records
      (let [state {:stage stage :route route :needs "none"}]
        (if (flow/to-refusal state to (contains? flagged stage) records)
          (recur stage records more)
          (let [next (flow/next-stage state to)]
            (recur next (conj records (handoff-record stage next)) more)))))))

(describe "send-backs"
  (it "count the handoffs whose target comes before their sender on the route"
    (should (passes? (prop/for-all [[r records] routed-records]
                       (= (count (filter #(earlier-on? r (:stage %) (:from %)) records))
                          (flow/send-backs records r))))))

  (it "ignore records that are not handoffs"
    (should (passes? (prop/for-all [[r records] routed-records
                                    type (gen/elements ["question" "answer" "review" "changes-requested"])]
                       (zero? (flow/send-backs (mapv #(assoc % :record type) records) r)))))))

(describe "to-refusal with send-back"
  (it "keeps the forward-only rule for a stage without the flag, whatever the records"
    (should (passes? (prop/for-all [[r records] routed-records
                                    stage-at gen/nat
                                    to (gen/elements (conj review-catalogue "done" ""))]
                       (let [roles (flow/route-list r)
                             state {:stage (nth roles (mod stage-at (count roles))) :route r :needs "none"}]
                         (= (flow/to-refusal state to) (flow/to-refusal state to false records)))))))

  (it "widens the rule only to earlier roles other than the specifier, under the cap"
    (should (passes? (prop/for-all [[r records] routed-records
                                    stage-at gen/nat
                                    to (gen/elements (conj review-catalogue "done" ""))]
                       (let [roles (flow/route-list r)
                             stage (nth roles (mod stage-at (count roles)))
                             state {:stage stage :route r :needs "none"}
                             back? (and (some #{to} roles) (earlier-on? r to stage))]
                         (if back?
                           (= (and (not= "specifier" to) (< (flow/send-backs records r) flow/send-back-cap))
                              (nil? (flow/to-refusal state to true records)))
                           (= (flow/to-refusal state to) (flow/to-refusal state to true records))))))))

  (it "never lets a task collect more send-backs than the cap, nor one by an unflagged stage"
    (should (passes? (prop/for-all [r review-route
                                    flagged (gen/set (gen/elements review-catalogue))
                                    targets (gen/vector (gen/elements (conj review-catalogue "done")) 0 40)]
                       (let [records (walk r flagged targets)
                             backs (filter #(earlier-on? r (:stage %) (:from %)) records)]
                         (and (<= (flow/send-backs records r) flow/send-back-cap)
                              (every? #(contains? flagged (:from %)) backs)
                              (not-any? #(= "specifier" (:stage %)) backs))))))))
