using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using OrbitalRift;

// Run in an isolated project prepared by Run-TravelPresentationRegression.ps1.
public static class TravelPresentationRegression
{
    public static void Run()
    {
        try
        {
            CheckTransitions();
            CheckGpu();
            Debug.Log("PASS: travel state transitions and jump GPU rendering.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void CheckTransitions()
    {
        var controller = new TravelUnderTest { playing = true };
        Tick(controller, 180);
        Require(Mathf.Abs(controller.spaceTravelSpeed - 1f) < .02f, "Classic cruise");
        controller.bossAlive = true;
        Tick(controller, 150);
        Require(Mathf.Abs(controller.spaceTravelSpeed - StarStreamSettings.CombatTravelSpeed) < .002f, "Boss arrival slows field");
        controller.bossAlive = false;
        Tick(controller, 20);
        Require(controller.spaceTravelSpeed > 2f && controller.musicReactiveVisuals.jump > .5f, "Boss victory triggers jump");
        var time = controller.backgroundTravelTime;
        var timer = controller.jumpTimer;
        controller.paused = true;
        controller.UpdateSpaceTravel(0f);
        Require(controller.backgroundTravelTime == time && controller.jumpTimer == timer, "Pause freezes travel clocks");
        controller.paused = false;
        controller.playing = false;
        Tick(controller, 1);
        Require(controller.jumpTimer == 0f && !controller.musicReactiveVisuals.active, "Exit stops jump");

        controller = new TravelUnderTest { coopPlaying = true, coopLocalPreview = true,
            coopPreviewEnemyKind = (byte)SectorRoomType.Combat, coopPreviewEnemyHealth = 100 };
        Tick(controller, 160);
        Require(controller.spaceTravelSpeed < .07f, "Solo expedition combat slows field");
        controller.coopPreviewEnemyHealth = 0;
        Tick(controller, 20);
        Require(controller.spaceTravelSpeed > 2f, "Solo expedition clear triggers jump");
        controller.coopPreviewEnemyKind = (byte)SectorRoomType.Shop;
        controller.coopPreviewEnemyHealth = 100;
        Tick(controller, 180);
        Require(controller.spaceTravelSpeed > .98f, "Shop is not a combat encounter");

        controller = new TravelUnderTest { coopPlaying = true, coopLocalPreview = false,
            coopSimulation = new SimulationUnderTest { CoopEnemyKind = (byte)SectorRoomType.Elite, CoopEnemyHealth = 100 } };
        Tick(controller, 160);
        Require(controller.spaceTravelSpeed < .07f, "Network co-op combat slows field");
        controller.coopSimulation.CoopEnemyHealth = 0;
        Tick(controller, 20);
        Require(controller.spaceTravelSpeed > 2f, "Network co-op clear triggers jump");
        controller.coopSimulation.RunFailed = true;
        Tick(controller, 1);
        Require(controller.jumpTimer == 0f && !controller.musicReactiveVisuals.active, "Failure does not trigger victory jump");
        Require(!OrbitSettings.ShowTrajectory, "Trajectory is hidden");
        Require(StarStreamSettings.NearTrailSeconds > StarStreamSettings.FarTrailSeconds &&
            StarStreamSettings.NearTrailWidth > StarStreamSettings.FarTrailWidth, "Near trails are longer and wider");
    }

    static void Tick(TravelUnderTest controller, int frames)
    { for (var i = 0; i < frames; i++) controller.UpdateSpaceTravel(1f / 60f); }
    static void Require(bool ok, string label)
    { if (!ok) throw new Exception(label); Debug.Log("PASS: " + label); }

    static void CheckGpu()
    {
        Directory.CreateDirectory("Results");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/After.shader");
        Require(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Jump shader compiles");
        var material = new Material(shader);
        material.SetVector("_MirrorBreak", new Vector4(-1, 0, 0, 0));
        material.SetColor("_NebulaPrimary", new Color(.16f, .48f, .85f));
        material.SetColor("_NebulaSecondary", new Color(.32f, .16f, .62f));
        material.SetColor("_NebulaSpark", new Color(.52f, .88f, 1f));
        material.SetFloat("_RippleReach", .36f);
        var impacts = new Vector4[6];
        for (var i = 0; i < 6; i++) impacts[i].z = -1;
        material.SetVectorArray("_Impacts", impacts);
        material.SetVectorArray("_ImpactColors", new Vector4[6]);
        foreach (var aspect in new[] { new Vector2Int(720, 1280), new Vector2Int(1280, 720) })
        foreach (var music in new[] { false, true })
        for (var frame = 0; frame < 6; frame++)
        {
            var jump = frame / 5f;
            material.SetVector("_Flight", new Vector4(jump, 9f + jump, 1f, 0f));
            material.SetVector("_Music", music ? new Vector4(.7f, .5f, .42f, .3f) : Vector4.zero);
            material.SetFloat("_TimePhase", 9f + jump);
            material.SetFloat("_WarpStrength", music ? .6f : 0f);
            var source = RenderTexture.GetTemporary(aspect.x, aspect.y, 0, RenderTextureFormat.ARGBFloat);
            var target = RenderTexture.GetTemporary(aspect.x, aspect.y, 0, RenderTextureFormat.ARGBFloat);
            Graphics.Blit(Texture2D.blackTexture, source);
            Graphics.Blit(source, target, material);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var readback = new Texture2D(aspect.x, aspect.y, TextureFormat.RGBAFloat, false);
            readback.ReadPixels(new Rect(0, 0, aspect.x, aspect.y), 0, 0);
            readback.Apply();
            var pixels = readback.GetPixels();
            var sum = 0f;
            foreach (var p in pixels)
            {
                if (float.IsNaN(p.r) || float.IsInfinity(p.r) || float.IsNaN(p.g) || float.IsInfinity(p.g) || float.IsNaN(p.b) || float.IsInfinity(p.b))
                    throw new Exception("Invalid jump GPU pixel");
                sum += p.r + p.g + p.b;
            }
            Require(sum > 1f, "Visible procedural nebula music=" + music + " jump=" + jump + " size=" + aspect);
            if (!music && (frame == 0 || frame == 5))
            {
                var image = new Texture2D(aspect.x, aspect.y, TextureFormat.RGBA32, false);
                for (var i = 0; i < pixels.Length; i++) pixels[i].a = 1f;
                image.SetPixels(pixels); image.Apply();
                File.WriteAllBytes("Results/" + aspect.x + "-" + (frame == 0 ? "cruise" : "jump") + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            RenderTexture.active = previous == source || previous == target ? null : previous;
            UnityEngine.Object.DestroyImmediate(readback);
            RenderTexture.ReleaseTemporary(source); RenderTexture.ReleaseTemporary(target);
        }
        UnityEngine.Object.DestroyImmediate(material);
    }
}
