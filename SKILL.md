---
name: create-internship-deliverables
description: Create self-contained internship or project-task deliverables that remain useful after source-code access is lost. Use when Codex should archive a completed or ongoing development task, preserve the complete logic and code chain, generate learning notes, summarize architecture and effects, produce resume bullets, prepare STAR interview answers, or update an existing task archive before leaving a company or project.
---

# Create Internship Deliverables

Produce a durable, evidence-based task archive. Assume the user may later have no repository, environment, chat history, or colleague available.

## Required Workflow

1. Read `references/deliverable-spec.md` and follow its output contract.
2. Read `references/logic-chain-capture.md` before inspecting or documenting code.
3. Inspect the current task, relevant source, diffs, configurations, protocols, tests, and history while access still exists.
4. Create exactly one deliverable at `E:/实习产出/<project-name>/<project-name>-项目复盘.md` unless the user gives another location.
5. Do not create supporting Markdown files, numbered chapters, README files, evidence files, or alternate versions for the same project.
6. Make `<project-name>-项目复盘.md` self-contained. Do not rely on clickable source paths as the only explanation.
7. Preserve a complete logic chain from external trigger to final result, including representative sanitized code or faithful pseudocode.
8. Read `references/output-templates.md` and use the single-document template.
9. Read `references/resume-interview.md` before writing resume or interview material.
10. Run the completeness gate in `references/deliverable-spec.md`; keep investigating until every applicable item is answered or explicitly marked unknown.

## Non-Negotiable Logic Requirement

Never finish with only a feature summary or file list. Record enough detail for a capable developer to reconstruct the design without the original source:

- entry and trigger;
- ordered call chain with class and method responsibilities;
- core data structures and important fields;
- state changes and lifecycle;
- configuration, network, persistence, async, and platform paths;
- success, failure, retry, deduplication, and edge branches;
- key code excerpts or sanitized pseudocode;
- final observable result and verification evidence.

If any link is missing, label the gap and inspect further. Do not invent missing code.

## Confidentiality

Preserve logic, not secrets. Remove credentials, tokens, personal data, private endpoints, internal hostnames, unreleased assets, and unnecessary proprietary code. Prefer minimal representative snippets and accurate pseudocode when output may be published. State what was sanitized.

## Quality Rules

- Distinguish verified facts, reasoned conclusions, and unknowns.
- Separate engineering effects from unverified business metrics.
- Explain why decisions were made and what alternatives or tradeoffs existed.
- Include mistakes, risks, limitations, and possible improvements.
- Use diagrams or tables only when they materially clarify a multi-step chain.
- Update the existing `<project-name>-项目复盘.md` instead of creating conflicting duplicate versions.
