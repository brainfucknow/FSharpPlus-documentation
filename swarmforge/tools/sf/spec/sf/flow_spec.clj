(ns sf.flow-spec
  (:require [speclj.core :refer [describe it should= should-be-nil should should-not]]
            [sf.flow :as flow]))

(def ^:private at-coder {:stage "coder" :route "specifier,coder,cleaner" :needs "none"})

(describe "route-list"
  (it "splits a route at its commas"
    (should= ["coder" "qa"] (flow/route-list "coder,qa")))

  (it "is empty for a blank route"
    (should= [] (flow/route-list ""))
    (should= [] (flow/route-list nil))))

(describe "waiting?"
  (it "is false when the task needs no one"
    (should-not (flow/waiting? at-coder)))

  (it "is true when the task needs the operator or an approval"
    (should (flow/waiting? (assoc at-coder :needs "operator")))
    (should (flow/waiting? (assoc at-coder :needs "approval")))))

(describe "next-stage"
  (it "is the role after the stage on the route"
    (should= "cleaner" (flow/next-stage at-coder nil)))

  (it "is done after the last role"
    (should= "done" (flow/next-stage (assoc at-coder :stage "cleaner") "")))

  (it "is the role that --to names"
    (should= "qa" (flow/next-stage at-coder "qa"))))

(describe "reviewed-commit"
  (it "is the commit the newest review names"
    (should= "0123456789" (flow/reviewed-commit [{:record "review" :commit "aaaaaaa" :reviewed "aaaaaaa000"}
                                                 {:record "review" :commit "bbbbbbb" :reviewed "0123456789"}
                                                 {:record "approval" :commit "bbbbbbb"}])))

  (it "is the commit under a review that names none"
    (should= "bbbbbbb" (flow/reviewed-commit [{:record "review" :commit "bbbbbbb"}]))))

(describe "approval of a moved review"
  (it "accepts the reviewed commit, full or abbreviated"
    (should-be-nil (flow/approval-refusal {:needs "approval"} "0123456789abcdef" "0123456")))

  (it "refuses a head that is not the reviewed commit"
    (should= "specification changed since review; run sf review again"
             (flow/approval-refusal {:needs "approval"} "0123456789abcdef" "fedcba9"))))

(describe "to-refusal"
  (it "lets --to name the stage or a later role on the route"
    (should-be-nil (flow/to-refusal at-coder nil))
    (should-be-nil (flow/to-refusal at-coder "coder"))
    (should-be-nil (flow/to-refusal at-coder "cleaner")))

  (it "lets --to name done only after the route's last role"
    (should-be-nil (flow/to-refusal (assoc at-coder :stage "cleaner") "done"))
    (should= "--to done skips the rest of route 'specifier,coder,cleaner' after stage:coder; hand off to the next role"
             (flow/to-refusal at-coder "done")))

  (it "refuses an earlier role and a role off the route"
    (should= "--to specifier is not on route 'specifier,coder,cleaner' at or after stage:coder"
             (flow/to-refusal at-coder "specifier"))
    (should= "--to qa is not on route 'specifier,coder,cleaner' at or after stage:coder"
             (flow/to-refusal at-coder "qa"))))

(def ^:private review-route "coder,cleaner,pr-reviewer,qa")

(def ^:private at-reviewer {:stage "pr-reviewer" :route review-route :needs "none"})

(defn- handoff-record [from to] {:record "handoff" :from from :stage to})

(def ^:private one-send-back
  [(handoff-record "coder" "cleaner") (handoff-record "cleaner" "pr-reviewer")
   (handoff-record "pr-reviewer" "coder")])

(def ^:private two-send-backs
  (into one-send-back [(handoff-record "coder" "cleaner") (handoff-record "cleaner" "pr-reviewer")
                       (handoff-record "pr-reviewer" "coder")]))

(describe "send-backs"
  (it "counts the handoff records whose target comes before their sender on the route"
    (should= 0 (flow/send-backs [] review-route))
    (should= 1 (flow/send-backs one-send-back review-route))
    (should= 2 (flow/send-backs two-send-backs review-route)))

  (it "skips other records, a handoff to the sender's own stage, and a sender off the route"
    (should= 0 (flow/send-backs [{:record "question" :from "pr-reviewer" :stage "coder"}
                                 (handoff-record "coder" "coder")
                                 (handoff-record "operator" "coder")
                                 (handoff-record "specifier" "coder")]
                                review-route))))

(describe "to-refusal with send-back"
  (it "lets a flagged stage send the task back to an earlier role twice"
    (should-be-nil (flow/to-refusal at-reviewer "coder" true []))
    (should-be-nil (flow/to-refusal at-reviewer "coder" true one-send-back))
    (should-be-nil (flow/to-refusal at-reviewer "cleaner" true one-send-back)))

  (it "refuses the third send-back of a task"
    (should= "--to coder: task was sent back 2 times; ask the operator"
             (flow/to-refusal at-reviewer "coder" true two-send-backs)))

  (it "keeps forward targets open after two send-backs"
    (should-be-nil (flow/to-refusal at-reviewer "qa" true two-send-backs)))

  (it "refuses a send-back to the specifier"
    (should= "--to specifier: a task cannot go back to the specifier"
             (flow/to-refusal {:stage "pr-reviewer" :route "specifier,coder,pr-reviewer" :needs "none"}
                              "specifier" true [])))

  (it "keeps the refusal of today for a stage without the flag"
    (should= "--to coder is not on route 'coder,cleaner,pr-reviewer,qa' at or after stage:cleaner"
             (flow/to-refusal (assoc at-reviewer :stage "cleaner") "coder" false []))
    (should= (flow/to-refusal at-reviewer "coder")
             (flow/to-refusal at-reviewer "coder" false [])))

  (it "refuses a role off the route even for a flagged stage"
    (should= "--to architect is not on route 'coder,cleaner,pr-reviewer,qa' at or after stage:pr-reviewer"
             (flow/to-refusal at-reviewer "architect" true []))))

(describe "refusals"
  (it "refuses a handoff while the task waits on the operator"
    (should-be-nil (flow/handoff-refusal at-coder))
    (should= "task is waiting on the operator (needs:operator); handoff refused"
             (flow/handoff-refusal (assoc at-coder :needs "operator"))))

  (it "refuses an approval the task does not wait for"
    (should-be-nil (flow/approval-refusal (assoc at-coder :needs "approval")))
    (should= "task is not waiting for approval" (flow/approval-refusal at-coder)))

  (it "requires an operator approval to leave the specifier stage"
    (doseq [record ["created" "review" "changes-requested" "answer"]]
      (should= "specification requires operator approval; run sf review and sf approve"
               (flow/handoff-refusal {:stage "specifier" :needs "none" :record record})))
    (should-be-nil (flow/handoff-refusal {:stage "specifier" :needs "none"
                                        :record "approval" :from "operator" :commit "c1"} "c1")))

  (it "binds approval and handoff to the reviewed role commit"
    (let [pending {:stage "specifier" :needs "approval"}
          approved {:stage "specifier" :needs "none" :record "approval" :from "operator" :commit "c1"}]
      (should-be-nil (flow/approval-refusal pending "c1" "c1"))
      (should= "specification changed since review; run sf review again"
               (flow/approval-refusal pending "c1" "c2"))
      (should= "specification changed since approval; run sf review again"
               (flow/handoff-refusal approved "c2"))))

  (it "refuses a route without the current stage, unless the task is done"
    (should-be-nil (flow/route-refusal at-coder "coder,qa"))
    (should-be-nil (flow/route-refusal {:stage "done"} "qa"))
    (should= "route 'qa' does not contain the current stage 'coder'" (flow/route-refusal at-coder "qa"))))

(describe "records"
  (it "starts a task at the first role of its route"
    (should= {:record "created" :from "operator" :text "Add login\n\nUsers log in."
              :stage "coder" :route "coder,qa" :needs "none"}
             (flow/created "Add login" "Users log in." "coder,qa")))

  (it "carries the state into the next record under its own fields"
    (should= {:record "question" :from "coder" :stage "coder" :route "specifier,coder,cleaner"
              :needs "operator" :text "Which?"}
             (flow/follow (assoc at-coder :sha "abc" :text "old") "question" "coder"
                          {:text "Which?" :needs "operator"})))

  (it "hands the role's commit on with the summary"
    (should= {:record "handoff" :from "coder" :stage "cleaner" :route "specifier,coder,cleaner"
              :needs "none" :commit "abc1234" :text "built"}
             (flow/handoff at-coder "coder" "cleaner" "abc1234" " built ")))

  (it "says the work is complete when the summary is blank"
    (should= "coder work complete" (:text (flow/handoff at-coder "coder" "cleaner" "abc1234" nil))))

  (it "records the merge of a finished task"
    (should= {:record "finished" :from "operator" :stage "done" :route "coder" :needs "none"
              :merged-into "main"}
             (flow/finished {:route "coder"} "main")))

  (it "carries the task's gates into the next record, the handoff and the finished merge"
    (let [state (assoc at-coder :gates "falsifier")]
      (should= "falsifier" (:gates (flow/follow state "question" "coder" {:needs "operator"})))
      (should= "falsifier" (:gates (flow/handoff state "coder" "cleaner" "abc1234" nil)))
      (should= "falsifier" (:gates (flow/finished state "main")))))

  (it "carries no gates for a task that has none"
    (should-not (contains? (flow/handoff at-coder "coder" "cleaner" "abc1234" nil) :gates))
    (should-not (contains? (flow/finished at-coder "main") :gates))))

(describe "gates"
  (it "lists a task's gates, none as empty"
    (should= ["falsifier" "doubter"] (flow/gates-list "falsifier,doubter"))
    (should= [] (flow/gates-list nil)))

  (it "shows a task's gates, none when it has none"
    (should= "falsifier" (flow/gates-shown {:gates "falsifier"}))
    (should= "none" (flow/gates-shown {:route "coder"})))

  (it "takes none or gate roles, without duplicates, in catalogue order"
    (should-be-nil (flow/gates-value "none" ["falsifier"]))
    (should= "falsifier" (flow/gates-value "falsifier,falsifier" ["falsifier"]))
    (should= "falsifier,doubter" (flow/gates-value "doubter,falsifier" ["falsifier" "doubter"])))

  (it "refuses an empty value, an empty name and a name that is not a gate role"
    (should-be-nil (flow/gates-refusal "--gates" "none" ["falsifier"]))
    (should-be-nil (flow/gates-refusal "--gates" "falsifier" ["falsifier"]))
    (should= "--gates needs gate roles or none, got ''" (flow/gates-refusal "--gates" "" ["falsifier"]))
    (should= "--gates needs gate roles or none, got 'falsifier,'" (flow/gates-refusal "--gates" "falsifier," ["falsifier"]))
    (should= "--gates: 'qa' is not a gate role (see sf gates), got 'falsifier,qa'"
             (flow/gates-refusal "--gates" "falsifier,qa" ["falsifier"]))
    (should= "gates: 'none' is not a gate role (see sf gates), got 'none,falsifier'"
             (flow/gates-refusal "gates" "none,falsifier" ["falsifier"]))))

(def ^:private records
  [{:record "created" :sha "c1" :commit "c1"}
   {:record "handoff" :sha "h1" :commit "r1"}
   {:record "question" :sha "q1" :commit "r1"}
   {:record "finished" :sha "f1" :commit "m1"}])

(describe "commits"
  (it "is the tip, or the role's commit under the newest record at the tip"
    (should= "r2" (flow/role-commit "r2" (records 2)))
    (should= "r1" (flow/role-commit "q1" (records 2))))

  (it "finds the commit the branch started from"
    (should= "c1" (flow/created-commit records))
    (should-be-nil (flow/created-commit (rest records))))

  (it "finds the commit of the newest created, review or handoff record"
    (should= "r1" (flow/last-milestone-commit records))))

(describe "what sf finish does with a done task"
  (it "merges a branch that HEAD neither contains nor has the finished record of"
    (should= :merge (flow/finish-action false "t1" false)))

  (it "keeps HEAD when HEAD's history has the finished record and the branch tip"
    (should= :keep (flow/finish-action true "t1" true)))

  (it "stops when the branch moved after the recorded merge"
    (should= :unmerged (flow/finish-action true "t1" false)))

  (it "records the finished merge on HEAD when HEAD contains the branch tip without the record"
    (should= :record (flow/finish-action false "t1" true)))

  (it "refuses a task without a branch"
    (should-be-nil (flow/finish-action true nil false))
    (should-be-nil (flow/finish-action false nil true))))

(describe "operator commands"
  (it "are refused to a dispatched role or gate"
    (should= "approve is the operator's; the dispatched specifier cannot run it"
             (flow/operator-refusal "approve" "specifier")))

  (it "run when sf dispatch started nothing"
    (should-be-nil (flow/operator-refusal "approve" nil))
    (should-be-nil (flow/operator-refusal "approve" ""))))

(describe "operator-record"
  (it "finds the first record the operator wrote"
    (should= {:record "approval" :from "operator"}
             (flow/operator-record [{:record "review" :from "specifier"}
                                    {:record "approval" :from "operator"}
                                    {:record "handoff" :from "specifier"}])))

  (it "is nil when roles wrote every record"
    (should-be-nil (flow/operator-record [{:record "review" :from "specifier"}]))
    (should-be-nil (flow/operator-record []))))

