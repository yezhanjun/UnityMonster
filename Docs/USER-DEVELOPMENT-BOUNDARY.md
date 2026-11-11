# Development boundary

UnityMonster is the independent monster acceptance project. Local working copy: D:/Project/Unity/UnityMonster; remote: https://github.com/yezhanjun/UnityMonster.

The user is actively developing D:/Project/Unity/TPSBattle/TPSShoot. Treat that entire main project as read-only, including code, assets, documentation, GM sorting and Library. Do not merge changes there or control, close or restart the user's Unity editor.

The source snapshot was the old isolated project D:/Project/Unity/TPSBattle/Builds/MonsterWhitebox-01a1c6ef. Future monster work belongs in UnityMonster after the user resumes it. Preserve original GUIDs and screenshot evidence.

The monster Goal remains PAUSED. Do not resume development or additional monster acceptance runs without a user request. Repository migration included one compatibility run of already accepted CHILD_BODY, without increasing accepted count. Its individual behavior checks passed; aggregate smoke failed due to the same 14 known rendering keyword errors. See Validation/README.md.

Current recorded totals: 91 passed / 283; 0 failed; 191 pending; 1 non-attacking bat skipped. Main historical integrated baseline remains 88. No changes were written back to main during migration.

MiniMushroom and Child animation curves were baked and checked previously. Bell, ClayBruiser and Grenadier still need native-animation persistence follow-up after user resume. Do not claim all original animations or human listening checks passed.

Portable screenshot table: Acceptance/怪物实机截图验收汇总.html. Historical ledgers and source paths describe previous runs; this boundary and Acceptance/CURRENT-PROGRESS.json define current handover state.