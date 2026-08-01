# Bounty Board fixtures

This folder contains privacy-reviewed canonical 808 by 611 Bounty Board captures used by specialized detector regressions.

`BountyBoard_DimmedSlot1_01.png` through `BountyBoard_DimmedSlot4_01.png`, together with `BountyBoard_FourLiveOneDimmed_01.png`, cover a dimmed card at every horizontal slot and the fully dimmed `0/10` board. The cards remain visibly present, but only yellow Reroll or green Claim actions are live. Those actions own individual usable cards; the header count is not used to authorize clicks.

`BountyBoard_FieldOwnerVariant_01.png` is a privacy-safe derivative of a later field failure. Only the original annotated **Bounty Board** header crop and aligned **Back + Calendar** rail remain on an opaque black canonical canvas; all card contents, event rows, world imagery, identity, and mutable board artwork were removed. It protects Board ownership when the earlier bronze-body and selected-Event-row evidence changes.

`BountyBoard_FourLiveOneDimmed_01.png` also preserves exact action-anchored `#1`, `#2`, `#6`, and `#10` suffixes across four randomized paper placements; the wide `#10` title sits 46 pixels left and 87 pixels above its live reroll action.

`BountyBoard_NumberRasterVariant_01.png` through `BountyBoard_NumberRasterVariant_04.png` are privacy-reviewed full-client game captures from separate beta.49 through beta.52 failures. They preserve live Board/action ownership and the distinct post-Cancel `#5`, `#6`, `#4`, and `#9` rasterizations that differed from the canonical suffix templates while remaining unambiguous. Exact reviewed variants compete under the same one-pixel distance and runner-up margin as the canonical suffixes; they never become a general OCR path or bypass card ownership. The captures contain only Roblox game UI and no account identity, webhook, desktop, or private overlay.

The Board itself is owned by the stable header plus the two-button rail. The per-card production rule is position-independent and does not assume that the last card is the dimmed one. After a claim, two fresh Board observations must agree on the slot's settlement: no yellow or green action means dimmed and removes that number from the UTC-day reroll pool, while a live yellow Reroll action keeps the number available.

`WaveCounterNoVoice.png` is a privacy-safe derivative of a canonical no-voice match capture. It retains only the top status badges, bottom hotbar, and right-side Unit Manager/Stage Info controls on an opaque black canvas. The wave digit remains at its observed `(421,28)` origin, and the other retained regions provide independent gameplay-HUD ownership without preserving the world, player identity, or Settings contents.

`WaveCounterNoVoiceBrightScene.png` is a privacy-safe derivative of a beta.55 Infinite run whose supported `(421,28)` counter was rejected on every observation. The faint pill transparency preserves the bright map underneath, while the retained top status group, bottom hotbar, and right-side Unit Manager/Stage Info controls contain no account identity. It protects local rail contrast without allowing the changing scene to own the counter.

`WaveCounterLegacy.png` combines the field-captured retained top status layout with the same privacy-safe gameplay-owner regions. The wave digit remains at `(389,48)`. Both wave fixtures preserve the dark counter pill, blue Wave badge, neutral Wave label, and independently owned Stage controls; no account identity or private overlay is retained.

`WaveCounterType3.png` is a privacy-safe derivative of a third canonical top-bar layout. It retains the field-captured status pills with the wave digit at `(386,28)` plus the same independent gameplay-owner regions. The missing optional top-bar control shifts the status group left without changing the glyph scale; world and identity pixels outside those bounded regions are blacked out.
