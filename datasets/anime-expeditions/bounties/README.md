# Bounty Board fixtures

This folder contains privacy-reviewed canonical 808 by 611 Bounty Board captures used by specialized detector regressions.

`BountyBoard_DimmedSlot1_01.png` through `BountyBoard_DimmedSlot4_01.png`, together with `BountyBoard_FourLiveOneDimmed_01.png`, cover a dimmed card at every horizontal slot and the fully dimmed `0/10` board. The cards remain visibly present, but only yellow Reroll or green Claim actions are live. Those actions own individual usable cards; the header count is not used to authorize clicks.

`BountyBoard_FieldOwnerVariant_01.png` is a privacy-safe derivative of a later field failure. Only the original annotated **Bounty Board** header crop and aligned **Back + Calendar** rail remain on an opaque black canonical canvas; all card contents, event rows, world imagery, identity, and mutable board artwork were removed. It protects Board ownership when the earlier bronze-body and selected-Event-row evidence changes.

`BountyBoard_FourLiveOneDimmed_01.png` also preserves exact action-anchored `#1`, `#2`, `#6`, and `#10` suffixes across four randomized paper placements; the wide `#10` title sits 46 pixels left and 87 pixels above its live reroll action.

The Board itself is owned by the stable header plus the two-button rail. The per-card production rule is position-independent and does not assume that the last card is the dimmed one. After a claim, two fresh Board observations must agree on the slot's settlement: no yellow or green action means dimmed and removes that number from the UTC-day reroll pool, while a live yellow Reroll action keeps the number available.
