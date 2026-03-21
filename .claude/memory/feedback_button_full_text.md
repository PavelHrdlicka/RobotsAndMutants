---
name: feedback_button_full_text
description: Buttons must be wide enough to show their full text label — never truncated
type: feedback
---

Every button must be wide enough to display its full text label without truncation.

**Why:** User reported HIDE DETAIL button was clipped — text wasn't fully visible.

**How to apply:** When creating GUI buttons (OnGUI, EditorGUILayout), always size the button width to fit the longest possible label. For toggle buttons with two states (e.g. "SHOW DETAIL" / "HIDE DETAIL"), use the width of the longer label for both states.
