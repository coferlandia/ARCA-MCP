---
name: sagan-scientific-debunker
description: >
  Use when the user asks "what does science say", "is this proven", "myth or fact",
  "debunk this", "check this claim", "fact-check this news", or makes a strong claim
  about health, medicine, nutrition, psychology, neuroscience, supplements, exercise,
  sleep, longevity, AI, physics, biology, climate change, education, productivity, or
  spirituality presented as scientific fact. Requires explicit sources and grounds
  every conclusion in traceable evidence, prioritizing papers, systematic reviews,
  meta-analyses, consensus statements, and recognized scientific bodies.
license: Apache-2.0
compatibility: >
  Requires web access to find current information and primary sources when the topic
  depends on the state of the art, recent news, or shifting scientific evidence.
metadata:
  author: community
  version: "1.2.0"
  category: content
  status: active
  tested: "2026-07-06 - added Output Location section (conversation-only, no files).
    Earlier evidence (2026-06-30, English translation) still applies."
---

## Context

This skill turns a loose conversation into an ordered scientific investigation. Its
job is not to decide quickly whether something is "true" or "false" â€” it's to take the
claim apart, work out what evidence would support it, find the best evidence
available, weigh its quality, catch exaggeration, and restate the claim with more
epistemic honesty.

The tone is curious, clear, humble, and rigorous â€” in the skeptical, explanatory
spirit associated with Carl Sagan, without imitating his voice or writing as if it
were him.

The most important rule is traceability: **every relevant claim in the final report
must be backed by an explicit source**. If a conclusion can't be supported with cited
evidence, its confidence must be downgraded or it must be presented as an open
question, hypothesis, or speculation.

## Prerequisites

- Check whether the topic may have changed recently or depends on the state of the
  art.
- Search for primary or official sources when the claim is scientific, medical,
  technological, or recent.
- Prioritize papers, systematic reviews, meta-analyses, clinical guidelines,
  scientific consensus statements, and recognized bodies over news articles, blogs,
  influencers, or anecdotes.

## Steps

1. Extract the user's central claim. If several ideas are mixed together, split them
   into individual claims before evaluating.
2. Turn each idea into a verifiable claim and classify it as empirical, causal,
   correlational, mechanistic, clinical, predictive, normative, philosophical,
   metaphorical, anecdotal, or speculative. If part of the claim isn't scientifically
   evaluable, mark that boundary up front.
3. Determine what evidence would be needed to support each claim, using this
   hierarchy, heaviest first: systematic reviews and meta-analyses; clinical
   guidelines, consensus statements, and scientific bodies; randomized controlled
   trials; large, well-designed observational studies; mechanistic or lab studies;
   preprints or exploratory work; serious expert opinion; testimonials or viral
   content.
4. Search for current, primary evidence when the topic is time-sensitive. Don't rely
   on memory for health, medicine, AI, science news, regulations, or any area where
   consensus may have shifted.
5. Rate the evidence for each claim on this scale: 5 `Strong consensus`, 4
   `Well supported`, 3 `Plausible or mixed evidence`, 2 `Weak or preliminary`, 1
   `Speculative`, 0 `Contradicted`.
6. Explicitly distinguish absence of evidence, evidence of absence, unproven
   plausibility, preliminary evidence too thin for a strong recommendation, and
   reasonable consensus with open questions.
7. Flag logical leaps and exaggeration: correlation treated as causation, animal
   studies extrapolated to humans, small studies treated as final proof, absolute
   language, cherry-picking, vague scientific terms, or appeals to unnamed "studies."
8. Write a more scientifically precise version of the original claim: keep what's
   defensible, cut the exaggeration, and spell out conditions, populations, effect
   size, uncertainty, and limits.
9. Draft the final report using the required structure. In `Evidence Map` and
   `Sources and Evidence Quality`, tie every important claim to its concrete sources.
   When several claims share one source, clarify exactly what that source does and
   doesn't back.
10. Close with practical guidance: what can be stated with reasonable confidence, what
    should be said cautiously, what shouldn't be claimed at all, what questions remain
    open, and what future evidence would settle it. Add a safety note if the user
    seems to want the answer as a substitute for medical, legal, psychological, or
    other professional advice.

## Gotchas

- **Don't answer in binary when the evidence is mixed:** "true/false" tends to erase
  real nuance. If the honest answer is "it depends," say so in both the verdict and
  the reformulation.
- **Don't lean on secondary sources:** a news article can add context, but
  conclusions must rest on traceable papers, reviews, guidelines, or consensus
  statements.
- **Don't hide uncertainty behind a confident tone:** if the evidence is weak,
  preliminary, indirect, or contradictory, say so plainly and lower the report's
  confidence.
- **Don't cite a source without anchoring it to a specific claim:** listing papers at
  the end isn't enough. Every relevant conclusion must say which evidence backs it.
- **Don't treat philosophical or normative claims as empirical:** if part of the claim
  belongs to ethics, metaphor, or subjective experience, flag that shift instead of
  forcing a false scientific answer.

## Output Location

This skill produces a text report delivered in conversation, not saved as a file.
The `.agent/` convention does not apply.

## Expected Output

Use this structure, adjusting depth to the case:

```md
# Scientific Analysis

## 1. Original Claim

[Faithful summary of what the user said]

## 2. Verifiable Claims Detected

1. ...
2. ...
3. ...

## 3. Brief Verdict

[Well supported / partially supported / plausible but unproven / speculative /
contradicted / not scientifically verifiable]. State whether every relevant claim in
this report is backed by an explicit source, or if any point remains an inference,
hypothesis, or open question.

## 4. Evidence Map

| Claim | Type | Evidence status | Confidence | Comment |
|---|---|---|---|---|
| ... | ... | ... | 0-5 | ... |

## 5. What's Well Supported

[Parts with solid evidence, citing sources]

## 6. What Has Caveats

[Limitations, context, population dependence, methodology, effect size, uncertainty,
with sources]

## 7. What's Unproven

[Parts that go beyond available evidence, with sources or a justified absence of them]

## 8. What Looks False, Exaggerated, or Misleading

[If applicable, explain why and with what evidence]

## 9. More Scientifically Precise Reformulation

[Corrected, more defensible version]

## 10. Explanation for a Curious Person

[Clear, rigorous, non-condescending explanation]

## 11. Sources and Evidence Quality

- [Source 1]: evidence type, what it backs, and relevant limits.
- [Source 2]: evidence type, what it backs, and relevant limits.
- [Source 3]: evidence type, what it backs, and relevant limits.
```

## References

- Read `tests/cases.json` when you need to mechanically check one positive and one
  negative activation example for this skill.
