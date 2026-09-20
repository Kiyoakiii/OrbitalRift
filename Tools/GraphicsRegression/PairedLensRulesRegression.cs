using System;
using OrbitalRift;
using UnityEngine;

public static class PairedLensRulesRegression
{
    public static void Run(Action<bool, string> require)
    {
        for (var seed = 1; seed <= 1000; seed++)
        {
            require(PairedLensRules.TryCreatePair(seed, 1, out var pair), "Lens pair generated for seed " + seed);
            require(pair.IsValid && PairedLensRules.ValidatePlacement(pair), "Lens pair validates for seed " + seed);
            require(PairedLensRules.TryCreatePair(seed, 1, out var repeat) &&
                    Vector2.Distance(pair.FirstCenter, repeat.FirstCenter) < .0001f &&
                    Vector2.Distance(pair.SecondCenter, repeat.SecondCenter) < .0001f,
                "Lens generation is reproducible for seed " + seed);
        }

        // Normals point out of the two mouths. A transit must leave away from the
        // destination mouth, never turn back into the tunnel.
        var pairForTransit = new PairedLensPair(new Vector2(-1f, 0f), Vector2.left,
            new Vector2(1f, 0f), Vector2.right, PairedLensRules.LensRadius);
        var position = new Vector2(-.2f, 0f);
        var velocity = new Vector2(6f, 0f);
        var previous = new Vector2(-3f, 0f);
        var state = new PairedLensTransitState();
        var speed = velocity.magnitude;
        require(PairedLensRules.TryTransit(ref position, ref velocity, previous, PairedLensRules.PlayerShotRadius,
                ref state, pairForTransit, PairedLensRules.PlayerShotCooldown, out var transit),
            "Shot crosses first lens");
        var expectedExit = pairForTransit.SecondCenter + pairForTransit.SecondNormal *
            (pairForTransit.Radius + PairedLensRules.PlayerShotRadius + PairedLensRules.ExitEpsilon);
        require(Vector2.Distance(position, expectedExit) < .0001f && Vector2.Distance(transit.ExitPoint, expectedExit) < .0001f,
            "Lens exit uses radius plus object radius plus epsilon");
        require(Mathf.Abs(velocity.magnitude - speed) < .0001f && Vector2.Dot(velocity.normalized,
                pairForTransit.SecondNormal) > .999f,
            "Lens exits away from the destination mouth while preserving speed");
        require(state.Passes == 1 && state.Cooldown > 0f, "Lens records a limited cooldown");
        PairedLensRules.Tick(ref state, PairedLensRules.PlayerShotCooldown + .01f);
        require(state.Cooldown == 0f, "Cooldown is deterministic");
        state.Passes = PairedLensRules.MaxPasses;
        position = new Vector2(-.2f, 0f);
        velocity = new Vector2(6f, 0f);
        require(!PairedLensRules.TryTransit(ref position, ref velocity, previous, PairedLensRules.PlayerShotRadius,
                ref state, pairForTransit, PairedLensRules.PlayerShotCooldown, out _),
            "Third lens passage is blocked");

        var coreState = new PairedLensTransitState();
        position = new Vector2(-.1f, 0f);
        velocity = new Vector2(4f, 0f);
        require(PairedLensRules.TryTransit(ref position, ref velocity, previous, PairedLensRules.RelayCoreRadius,
                ref coreState, pairForTransit, PairedLensRules.RelayCoreCooldown, out _),
            "Relay core is eligible for a lens");
        require(coreState.Cooldown >= PairedLensRules.RelayCoreCooldown - .0001f,
            "Relay core receives the longer anti-loop cooldown");

        // The visual path must work in both headings. A reversed shot should still
        // leave the opposite lens instead of being rejected by orientation.
        var reverseState = new PairedLensTransitState();
        position = new Vector2(.2f, 0f);
        velocity = new Vector2(-6f, 0f);
        previous = new Vector2(3f, 0f);
        require(PairedLensRules.TryTransit(ref position, ref velocity, previous, PairedLensRules.PlayerShotRadius,
                ref reverseState, pairForTransit, PairedLensRules.PlayerShotCooldown, out var reverseTransit),
            "Shot crosses paired lens in reverse heading");
        require(Vector2.Distance(reverseTransit.EntryPoint, new Vector2(1.545f, 0f)) < .08f &&
                Vector2.Dot(velocity.normalized, pairForTransit.FirstNormal) > .999f,
            "Reverse entry is located on the second lens edge");

        // Regression for the reported top-to-bottom failure: vertical motion into
        // the upper mouth and bottom-to-top motion into the lower mouth both emit
        // from the other side, outwards. This test is independent of frame order.
        var topDownState = new PairedLensTransitState();
        position = new Vector2(1f, 2f);
        velocity = Vector2.down * 7f;
        previous = position;
        position += velocity * .40f;
        require(PairedLensRules.TryTransit(ref position, ref velocity, previous, PairedLensRules.PlayerShotRadius,
                ref topDownState, pairForTransit, PairedLensRules.PlayerShotCooldown, out var topDownTransit) &&
                !topDownTransit.EnteredFirst && Vector2.Dot(velocity.normalized, pairForTransit.FirstNormal) > .999f,
            "Top-to-bottom shot exits the lower mouth outwards");

        var bottomUpState = new PairedLensTransitState();
        position = new Vector2(-1f, -2f);
        velocity = Vector2.up * 7f;
        previous = position;
        position += velocity * .40f;
        require(PairedLensRules.TryTransit(ref position, ref velocity, previous, PairedLensRules.PlayerShotRadius,
                ref bottomUpState, pairForTransit, PairedLensRules.PlayerShotCooldown, out var bottomUpTransit) &&
                bottomUpTransit.EnteredFirst && Vector2.Dot(velocity.normalized, pairForTransit.SecondNormal) > .999f,
            "Bottom-to-top shot exits the upper mouth outwards");
    }

}
