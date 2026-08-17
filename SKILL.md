---
name: review-jinhua-activity-tracking
description: Review, explain, audit, extend, or prepare interview material for the JinHua Unity activity exposure, participation, and recharge tracking system. Use when working with ActivityTrackManager, activity tracking web APIs, Promotion/EPromotion activity attribution, recharge-order tracking, HD活动控制表.xlsx, tracking coverage, deduplication, reliability, resume bullets, or interview answers about this project.
---

# Review JinHua Activity Tracking

Base every explanation on the current repository at `E:/JinHua`; do not rely only on this snapshot when code may have changed.

## Workflow

1. Read `references/task-overview.md` to recover the business goal, scope, result, and configuration relationship.
2. Read `references/implementation.md` before explaining code, debugging, extending tracking, or reviewing data correctness.
3. Read `references/interview-guide.md` when preparing a resume, self-introduction, project story, or interviewer Q&A.
4. Inspect the current versions and diffs of the files named in those references.
5. Separate verified engineering effects from unverified business metrics. Never invent conversion-rate or revenue improvements.
6. When code and this skill disagree, treat code and backend contracts as authoritative and update the skill afterward.

## Review Checklist

- Trace the three event types separately: exposure, participation, recharge.
- Identify the real trigger, request payload, endpoint, deduplication rule, success condition, and retry behavior.
- Confirm `promotion_id`, `is_operate`, activity time range, role, server, channel, and order attribution.
- Verify special activities that bypass the normal `Promotion` UI path.
- Check `xlsx/Datas/HD活动/HD活动控制表.xlsx`, generated `Residentactivities`, and `EPromotion.cs` for synchronization.
- Check recharge order registration before payment and confirmation after payment delivery.
- Preserve user changes and avoid editing generated Luban files directly.

## Expected Outputs

For a learning review, explain the system in this order:

1. Why the task was needed.
2. What architecture was chosen.
3. How each event travels from trigger to backend.
4. How duplicate reporting and payment loss are handled.
5. What coverage and configuration boundaries remain.
6. What measurable engineering result was achieved.
7. How to describe the work in a resume and defend it in an interview.


