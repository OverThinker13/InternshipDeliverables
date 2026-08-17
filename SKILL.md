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
4. Determine the company from the user's prompt. Treat “当前公司” as `广州小画网络科技有限公司`; if no company can be determined safely, ask before writing.
5. Create exactly one deliverable at `E:/实习产出/项目复盘/<company-name>/<project-name>-项目复盘.md` unless the user gives another location.
6. Use the company name as the directory name and the stable project short name in the filename. Never merge different companies into one directory.
7. Do not create supporting Markdown files, numbered chapters, README files, evidence files, or alternate versions for the same project.
8. Make `<project-name>-项目复盘.md` self-contained. Do not rely on clickable source paths as the only explanation.
9. Preserve a complete logic chain from external trigger to final result, including representative sanitized code or faithful pseudocode.
10. Read `references/output-templates.md` and use the single-document template.
11. Read `references/resume-interview.md` before writing resume or interview material.
12. Run the completeness gate in `references/deliverable-spec.md`; keep investigating until every applicable item is answered or explicitly marked unknown.
13. Before GitHub synchronization, perform the confidentiality gate below. Stage only the intended project document and intentional Skill changes; never use broad staging when unrelated files exist.
14. Commit with a clear message, push `main` to the configured `origin`, then verify that local `HEAD` and `origin/main` resolve to the same commit. If push or verification fails, report it explicitly.

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

Assume the configured GitHub repository may be public. Before committing, search the new document for secrets and confidential material. Publish only sanitized project logic. Never push credentials, private endpoints, personal data, unreleased assets, or proprietary source beyond the minimum representative excerpts allowed by the user. When exact code is sensitive, replace it with faithful pseudocode and record the substitution.

## Storage and GitHub Synchronization

- The canonical tracked root is `E:/实习产出/InternshipDeliverablesSkill/项目复盘`.
- `E:/实习产出/项目复盘` may be a directory junction to that tracked root; treat both paths as the same files and never maintain duplicate copies.
- Current company mapping: `广州小画网络科技有限公司` → `项目复盘/广州小画网络科技有限公司`.
- For another company supplied in a future prompt, create `项目复盘/<company-name>` and keep the same one-project-one-document rule.
- Synchronize only after the document passes completeness and confidentiality checks.

## Quality Rules

- Distinguish verified facts, reasoned conclusions, and unknowns.
- Separate engineering effects from unverified business metrics.
- Explain why decisions were made and what alternatives or tradeoffs existed.
- Include mistakes, risks, limitations, and possible improvements.
- Use diagrams or tables only when they materially clarify a multi-step chain.
- Update the existing `<project-name>-项目复盘.md` instead of creating conflicting duplicate versions.
