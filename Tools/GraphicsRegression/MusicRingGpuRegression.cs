// Copy into Assets/Editor of an isolated Unity project with Resources/Before.shader and
// Resources/After.shader. Run -batchmode -force-d3d11 -executeMethod MusicRingGpuRegression.Run.
// GPU rendering stays enabled: -nographics would invalidate this regression.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MusicRingGpuRegression
{
    public static void Run()
    {
        try
        {
            var output = Path.GetFullPath("Results");
            Directory.CreateDirectory(output);
            ValidateRingPeriodicity();
            var before = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Before.shader"));
            var after = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/After.shader"));
            foreach (var material in new[] { before, after })
            {
                if (!material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader))
                    throw new Exception("Shader did not compile: " + material.shader.name);
                material.SetVector("_MirrorBreak", new Vector4(-1, 0, 0, 0));
                var impacts = new Vector4[6];
                for (var i = 0; i < impacts.Length; i++) impacts[i].z = -1;
                material.SetVectorArray("_Impacts", impacts);
                material.SetVectorArray("_ImpactColors", new Vector4[6]);
                material.SetColor("_NebulaPrimary", new Color(.3f, .6f, 1f));
                material.SetColor("_NebulaSecondary", new Color(1f, .2f, .05f));
                material.SetColor("_NebulaSpark", new Color(.6f, 1f, .9f));
                material.SetFloat("_WarpStrength", .6f);
            }
            var report = "GPU: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType + "\n";
            var worstBefore = 0f;
            var worstAfter = 0f;
            var cases = 0;
            foreach (var dimensions in new[] { new Vector2Int(1024, 1024), new Vector2Int(720, 1280), new Vector2Int(1280, 720) })
            for (var frame = 0; frame < 8; frame++)
            {
                var time = frame * 3.17f;
                foreach (var material in new[] { before, after })
                {
                    material.SetFloat("_TimePhase", time);
                    material.SetVector("_Music", new Vector4(.7f, .6f, .13f + frame * .105f, .4f));
                }
                var oldImage = Render(before, dimensions.x, dimensions.y);
                var newImage = Render(after, dimensions.x, dimensions.y);
                var oldSeam = HorizontalSeam(oldImage);
                var newSeam = HorizontalSeam(newImage);
                worstAfter = Mathf.Max(worstAfter, newSeam);
                if (oldSeam > worstBefore)
                {
                    worstBefore = oldSeam;
                    Save(oldImage, Path.Combine(output, "before.png"));
                    Save(newImage, Path.Combine(output, "after.png"));
                    SaveSeamComparison(oldImage, newImage, Path.Combine(output, "seam-comparison.png"));
                }
                report += dimensions + " t=" + time + " seam before=" + oldSeam + " after=" + newSeam + "\n";
                UnityEngine.Object.DestroyImmediate(oldImage);
                UnityEngine.Object.DestroyImmediate(newImage);
                cases++;
            }
            report += "Cases=" + cases + "; maximum adjacent-row RGB difference before=" + worstBefore + "; after=" + worstAfter + "\n";
            File.WriteAllText(Path.Combine(output, "report.txt"), report);
            Debug.Log(report);
            if (worstBefore < .01f) throw new Exception("The old seam was not reproduced.");
            if (worstAfter >= worstBefore * .25f || worstAfter > .008f)
                throw new Exception("A visible seam remains in the fixed shader.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static Texture2D Render(Material material, int width, int height)
    {
        var source = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(Texture2D.blackTexture, source);
            Graphics.Blit(source, target, material);
            RenderTexture.active = target;
            var result = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            foreach (var pixel in result.GetPixels())
                if (float.IsNaN(pixel.r) || float.IsInfinity(pixel.r) || float.IsNaN(pixel.g) || float.IsNaN(pixel.b))
                    throw new Exception("Non-finite GPU output.");
            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(source);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    static void ValidateRingPeriodicity()
    {
        // RingWaveUnderTest.cs is extracted verbatim from the production PeriodicWave method
        // by Run-MusicRingRegression.ps1, so this checks the code used by both LineRenderers.
        var worstValue = 0f;
        var worstTangent = 0f;
        const float epsilon = .002f;
        for (var sample = 0; sample < 500; sample++)
        {
            var frequency = 2.1f + sample * .017f;
            var phase = sample * .113f;
            foreach (var start in new[] { 0f, -Mathf.PI })
            {
                var end = start + Mathf.PI * 2f;
                var a = RingWaveUnderTest.PeriodicWave(start, frequency, phase);
                var b = RingWaveUnderTest.PeriodicWave(end, frequency, phase);
                worstValue = Mathf.Max(worstValue, Mathf.Abs(a - b));
                var da = (RingWaveUnderTest.PeriodicWave(start + epsilon, frequency, phase) -
                    RingWaveUnderTest.PeriodicWave(start - epsilon, frequency, phase)) / (2 * epsilon);
                var db = (RingWaveUnderTest.PeriodicWave(end + epsilon, frequency, phase) -
                    RingWaveUnderTest.PeriodicWave(end - epsilon, frequency, phase)) / (2 * epsilon);
                worstTangent = Mathf.Max(worstTangent, Mathf.Abs(da - db));
            }
        }
        Debug.Log("Ring periodicity: 1000 cases; value error=" + worstValue + "; tangent error=" + worstTangent);
        if (worstValue > .0001f || worstTangent > .02f)
            throw new Exception("Ring values or tangents do not meet at the closing segment.");
    }

    static float HorizontalSeam(Texture2D texture)
    {
        var largest = 0f;
        // Both plasma rims are on the left half of atan2's branch cut. Ignore the singular
        // centre and compare neighbouring rows on the GPU-rendered image, not a CPU formula.
        for (var x = 1; x < texture.width / 2 - 12; x++)
        {
            var a = texture.GetPixel(x, texture.height / 2 - 1);
            var b = texture.GetPixel(x, texture.height / 2);
            largest = Mathf.Max(largest, Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
        }
        return largest;
    }

    static void Save(Texture2D input, string path)
    {
        var image = new Texture2D(input.width, input.height, TextureFormat.RGBA32, false);
        var pixels = input.GetPixels();
        for (var i = 0; i < pixels.Length; i++) { pixels[i] *= 3f; pixels[i].a = 1f; }
        image.SetPixels(pixels);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    static void SaveSeamComparison(Texture2D before, Texture2D after, string path)
    {
        var width = before.width / 2;
        var image = new Texture2D(width * 2, 160, TextureFormat.RGBA32, false);
        for (var y = 0; y < 160; y++)
        for (var x = 0; x < width; x++)
        {
            var a = before.GetPixel(x, before.height / 2 - 80 + y) * 4f;
            var b = after.GetPixel(x, after.height / 2 - 80 + y) * 4f;
            a.a = b.a = 1f;
            image.SetPixel(x, y, a);
            image.SetPixel(x + width, y, b);
        }
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }
}
