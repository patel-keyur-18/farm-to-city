# NNN. Feature name

- Status: Draft | In review | Approved | Implemented
- Author: Claude
- Reviewer: Keyur Patel
- GDD sections: (the sections of `docs/GDD.md` this spec expands)
- Related ADRs: (for example ADR 0002)
- Depends on specs: (for example 001)

## 1. Goal

The player experience, in one sentence, and the lesson it teaches.

## 2. Scope

**In:**
- ...

**Out** (explicitly, with where it goes instead):
- ...

## 3. Rules and formulas

Give every number with its units and its bounds. Values can start as placeholders: the economy model tunes them in `content/` and the spec's defaults are then updated without re-approval. Formulas and rules need re-approval if they change (ADR 0027). Formulas use fixed-point or integer maths (ADR 0018), so state the rounding rule wherever division happens. Reference data values by their definition field, never by a hard-coded number.

| Rule | Formula or behaviour | Units | Bounds |
| --- | --- | --- | --- |
| ... | ... | ... | ... |

## 4. Data

**New or changed definitions** (static content in `content/*.json`):

| Definition | Field | Type | Default | Bounds | Notes |
| --- | --- | --- | --- | --- | --- |
| ... | ... | ... | ... | ... | ... |

**New or changed state** (saved in the game state):

| Entity | Field | Type | Notes |
| --- | --- | --- | --- |
| ... | ... | ... | ... |

## 5. Commands and events

**Commands** (what the player or the platform layer can do):

| Command | Parameters | Validation and rejection reasons | Effect |
| --- | --- | --- | --- |
| ... | ... | ... | ... |

**Domain events** (what the simulation announces):

| Event | Payload | Emitted when | Consumers |
| --- | --- | --- | --- |
| ... | ... | ... | ... |

## 6. Decision card

For each decision the player makes in this feature:

| Decision | Immediate cost | Delayed effect | Side effects on other systems | Reversibility |
| --- | --- | --- | --- | --- |
| ... | ... | ... | ... | ... |

## 7. UI

The screens touched, with a low-fidelity mock-up (ASCII is fine). Note the *Why?* breakdowns this feature adds. The UI must be buildable code-first (ADR 0017).

```
+--------------------------------------------------+
|                                                  |
+--------------------------------------------------+
```

## 8. Acceptance criteria

Testable statements, each mapped to an automated test.

| ID | Given / When / Then | Test |
| --- | --- | --- |
| AC-1 | Given ..., when ..., then ... | `TestClass.TestName` |

## 9. Balancing hooks

The balancer metrics this feature should move, and by how much.

| Metric | Expected effect | Tolerance |
| --- | --- | --- |
| ... | ... | ... |

## 10. Open questions

- ...

## 11. Cut first

If this spec runs over, drop these first, in order:

1. ...
