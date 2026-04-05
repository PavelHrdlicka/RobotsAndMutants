---
name: Two modes - Training vs Replay
description: Future plan for TRAINING mode (max speed, no graphics) and REPLAY mode (beautiful visuals for YouTube video recording)
type: project
---

Two planned modes: TRAINING and REPLAY.

**TRAINING**: Max speed, no graphics needed. Goal is to maximize hardware utilization for ML-Agents training.
- Options: --no-graphics, server build, disable visual components, high timeScale

**REPLAY**: Load saved JSONL replay files, watch games in high visual quality, record video for YouTube.
- Options: post-processing, better materials, smooth camera, Unity Recorder

**Why:** User wants to separate concerns — training speed vs visual quality for content creation.

**How to apply:** When implementing training optimizations, don't worry about visuals. When improving visuals, only apply to replay mode. Architecture already supports this (JSONL files bridge the two modes).
