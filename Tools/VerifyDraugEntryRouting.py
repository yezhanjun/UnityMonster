"""Verify source first-frame routing against serialized AnyState and local transitions.
This audits measured source Animator traces, not runtime combat equivalence.
"""
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
def read(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8-sig"))

graph_path = "Docs/Validation/DraugAnimatorGraphEvidence.json"
graph = read(graph_path)
any_attack = next(t for t in graph["anyStateTransitions"]
                  if t["destinationName"] == "base.attack.decide"
                  and any(c["parameter"] == "attack" for c in t["conditions"]))
assert any_attack["m_HasExitTime"] and any_attack["m_HasFixedDuration"]
assert any_attack["m_InterruptionSource"] == 0
exit_time = any_attack["m_ExitTime"]
assert 0 < exit_time < 1
any_hash = any_attack["m_FullPathID"]
local_hashes = {}
for state in graph["states"]:
    if state["name"] in ("base.idle", "base.chase"):
        transition = next(t for t in state["transitions"]
                          if t["destinationName"] == "base.attack.decide")
        local_hashes[state["name"]] = transition["m_FullPathID"]

inputs = ["Docs/Validation/DraugFrameStepTransitionIdentityVerification.json",
          "Docs/Validation/DraugAnyStateBoundaryVerification.json"]
counts = {"anyState": 0, "local": 0}
checked = 0
for path in inputs:
    for case in read(path)["cases"]:
        if case["entry"] not in local_hashes:
            continue
        before = case["beforeTrigger"]
        first = case["entryFrames"][0]
        assert not before["transitioning"], case
        assert first["currentState"] == case["entry"], case
        a, b = before["currentTime"], first["currentTime"]
        assert b >= a
        crossed = any(a < cycle + exit_time <= b
                      for cycle in range(math.floor(a), math.floor(b) + 1))
        expected = any_hash if crossed else local_hashes[case["entry"]]
        assert first["transitionHash"] & 0xffffffff == expected, case
        if crossed:
            assert first["durationUnit"] == "Fixed"
            assert abs(first["transitionDuration"] - any_attack["m_TransitionDuration"]) < 1e-7
        counts["anyState" if crossed else "local"] += 1
        checked += 1
assert checked == 184, checked
result = {"status": "PASS_SOURCE_FIRST_FRAME_ROUTING", "checked": checked,
          "counts": counts, "anyStateTransition": any_attack,
          "rule": "If the first evaluated frame crosses a loop-relative .05 exit boundary, the conditioned AnyState attack route wins; otherwise observed local idle/chase attack route wins.",
          "inputs": [{"path": p, "sha256": hashlib.sha256((ROOT / p).read_bytes()).hexdigest()}
                     for p in [graph_path] + inputs],
          "limitations": ["Only measured first-frame routing; source native callbacks/events absent.",
                         "Explicit Animator.Update timing; runtime PlayerLoop and pose chain not compared.",
                         "Rule verified over sampled phases/steps only, not arbitrary transition interruptions."]}
(ROOT / "Docs/Validation/DraugEntryRoutingVerification.json").write_text(
    json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("DRAUG_ENTRY_ROUTING_PASS", checked, counts)
