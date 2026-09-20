Shader "OrbitalRift/Music Space Water Distortion"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _MusicCenter ("Music center", Vector) = (.5,.5,0,0)
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _MusicCenter;
            float4 _Impacts[6];
            float4 _ImpactColors[6];
            float4 _Music;
            float4 _NebulaPrimary;
            float4 _NebulaSecondary;
            float4 _NebulaSpark;
            float _TimePhase;
            float _WarpStrength;
            // x = elapsed encounter time (-1 when inactive), y = intensity, z = layout seed.
            float4 _MirrorBreak;
            float4 _Flight; // jump envelope, integrated travel time, active run, reserved
            float4 _RegionShape; // cloud scale, density, shear, enabled (zero preserves legacy)
            float _RippleReach;

            float Square(float value) { return value * value; }

            // Blend whole harmonics: fractional angle multipliers do not meet at -PI/+PI.
            float PeriodicWave(float angle, float frequency, float phase)
            {
                float harmonic = floor(frequency);
                return lerp(sin(angle * harmonic + phase),
                    sin(angle * (harmonic + 1.0) + phase), frac(frequency));
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float lower = lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x);
                float upper = lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x);
                return lerp(lower, upper, f.y);
            }

            float SoftNebula(float2 p)
            {
                float value = Noise(p);
                value += Noise(p * 2.07 + 17.3) * .5;
                value += Noise(p * 4.11 - 9.4) * .24;
                return value / 1.74;
            }

            float StarDust(float2 uv, float time, float sideDirection)
            {
                // Tiny horizontally stretched particles give the side corridors a direction of
                // travel. They are deliberately absent around the orbit, where readability wins.
                float2 grid = float2(13.0, 31.0);
                float2 movingUv = uv * grid + float2(time * (.35 + _Music.x * .9) * sideDirection, 0.0);
                float2 cell = floor(movingUv);
                float2 local = frac(movingUv) - .5;
                float seed = Hash21(cell + float2(sideDirection * 19.0, 7.0));
                float streak = 1.0 - smoothstep(.012, .082 + seed * .055, abs(local.y));
                streak *= 1.0 - smoothstep(.05, .38, abs(local.x));
                return streak * step(.82, seed);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float2 musicUv = uv - _MusicCenter.xy + .5;
                float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float2 offset = 0;
                float3 impactGlow = 0;

                // An impact is a tiny expanding normal-map disturbance. The picture itself
                // bends along that normal, so a star, ship, or enemy visibly refracts instead
                // of merely receiving another ring drawn over it.
                [unroll]
                for (int index = 0; index < 6; index++)
                {
                    float progress = _Impacts[index].z;
                    if (progress < 0.0 || progress >= 1.0) continue;
                    float strength = _Impacts[index].w;
                    // The final frames must dissolve, not drop out on the exact lifetime edge.
                    float dissolve = 1.0 - smoothstep(.55, 1.0, progress);
                    // Let a wave arrive from a pinprick instead of popping over the rendered
                    // scene on the beat frame. The world and its UI stay in place throughout.
                    float arrival = smoothstep(0.0, .14, progress);
                    float2 delta = uv - _Impacts[index].xy;
                    float2 metricDelta = float2(delta.x * aspect, delta.y);
                    float distanceToImpact = length(metricDelta) + .0001;
                    float travel = 1.0 - pow(1.0 - saturate(progress / .78), 1.18);
                    float radius = .008 + travel * max(_RippleReach, .10 + strength * .20);
                    float width = lerp(.016, .052, progress);
                    // pow(negative, 2) is undefined on some shader backends. Signed distances
                    // must be squared explicitly or entire inner bands can become NaN.
                    float edge = exp(-Square((distanceToImpact - radius) / width) * 2.8);
                    float ripple = sin((distanceToImpact - radius) * 155.0 - progress * 12.0) * edge * dissolve;
                    float core = exp(-distanceToImpact * distanceToImpact / (.00035 + strength * .0008)) * exp(-progress * 14.0);
                    float2 direction = metricDelta / distanceToImpact;
                    direction.x /= aspect;
                    float2 tangent = float2(-direction.y, direction.x);
                    offset += direction * ripple * (.0020 + strength * .0080) * (1.0 - progress * .34) * arrival;
                    offset += direction * core * (.0025 + strength * .0040) * dissolve * arrival;
                    // The tangential component is small but critical: stars and enemies curl
                    // around the impact like water, instead of simply pulsing in place.
                    offset += tangent * edge * (.0007 + strength * .0022) * (1.0 - progress) * dissolve * arrival;
                    // The screen-space counterpart of the visible rain ripple: a clear wet
                    // highlight that follows the refracted ring, rather than a generic bloom.
                    impactGlow += _ImpactColors[index].rgb * (edge * .24 + core * .26) * strength * dissolve * arrival;
                }

                // Music does not shake the central play space. It flows sideways around it:
                // coloured nebula banks occupy the left/right edge.
                float side = abs(musicUv.x - .5) * 2.0;
                float sideMask = smoothstep(.30, .98, side);
                float topBottomFade = 1.0 - smoothstep(.18, 1.12, abs(musicUv.y - .5) * 2.0);
                float topBottom = abs(musicUv.y - .5) * 2.0;
                float topBottomMask = smoothstep(.44, .98, topBottom);
                float perimeterMask = max(sideMask, topBottomMask);
                // Side banks move vertically while upper/lower banks sweep horizontally. The
                // two flows make the rim feel like a volume being flown through on every edge.
                float2 driftingCloudUv = musicUv * float2(3.55, 6.3) + float2(_TimePhase * .018, -_TimePhase * .031);
                float2 rimCloudUv = musicUv * float2(5.8, 3.25) + float2(-_TimePhase * .036, _TimePhase * .014);
                float regionScale = lerp(1.0, _RegionShape.x, _RegionShape.w);
                driftingCloudUv = driftingCloudUv * regionScale + float2(musicUv.y * _RegionShape.z * _RegionShape.w, 0.0);
                rimCloudUv = rimCloudUv * regionScale + float2(0.0, musicUv.x * _RegionShape.z * _RegionShape.w);
                float jump = saturate(_Flight.x);
                float2 flightPoint = float2((musicUv.x - .5) * aspect, musicUv.y - .5);
                float flightRadius = length(flightPoint);
                float2 flightDirection = flightPoint / max(.0001, flightRadius);
                // A radial coordinate makes the coloured banks stretch from the vanishing
                // point during the jump, with no angular wrap or discrete texture tiles.
                float radialStretch = lerp(7.0, .26, jump);
                float2 flightCloudUv = flightDirection * 3.2 +
                    float2(flightRadius * radialStretch - _Flight.y * .20, _Flight.y * .035);
                driftingCloudUv = lerp(driftingCloudUv, flightCloudUv, jump);
                rimCloudUv = lerp(rimCloudUv, flightCloudUv * 1.17 + 13.7, jump);
                float cloudNoise = SoftNebula(driftingCloudUv);
                float rimCloudNoise = SoftNebula(rimCloudUv + 13.7);
                float cloudBand = smoothstep(.34, .75, cloudNoise) * sideMask * topBottomFade;
                float rimCloudBand = smoothstep(.39, .76, rimCloudNoise) * topBottomMask;
                float sideDirection = musicUv.x < .5 ? -1.0 : 1.0;
                float rimDirection = musicUv.y < .5 ? -1.0 : 1.0;
                float flow = sin(musicUv.y * (15.0 + _Music.z * 10.0) + _TimePhase * (.62 + _Music.x * .75) + cloudNoise * 6.0);
                float rimFlow = sin(musicUv.x * (13.0 + _Music.y * 8.0) - _TimePhase * (.70 + _Music.x * .68) + rimCloudNoise * 5.4);
                float lateralFlow = sideMask * (.00055 + _Music.x * .0028 + _Music.y * .0016);
                offset += float2(sideDirection * lateralFlow * flow, lateralFlow * .42 * cos(musicUv.x * 18.0 + _TimePhase * .43));
                float rimFlowAmount = topBottomMask * (.00042 + _Music.x * .0021 + _Music.z * .0012);
                offset += float2(rimFlowAmount * .46 * sin(musicUv.y * 15.0 - _TimePhase * .38), rimDirection * rimFlowAmount * rimFlow);

                float2 sampleUv = uv + offset * _WarpStrength;
                float mirrorCrack = 0.0;
                float mirrorGlow = 0.0;
                if (_MirrorBreak.x >= 0.0)
                {
                    // Five Voronoi cells give the whole encounter a broken-mirror layout rather
                    // than a uniform shake. Each shard samples a visibly different part of the
                    // already rendered world, so enemies, projectiles and arena all refract.
                    float nearest = 10.0;
                    float secondNearest = 10.0;
                    float2 nearestSeed = 0;
                    [unroll]
                    for (int shard = 0; shard < 5; shard++)
                    {
                        float shardId = (float)shard;
                        float2 seedPoint = float2(
                            Hash21(float2(shardId * 11.73 + _MirrorBreak.z, 4.17)),
                            Hash21(float2(shardId * 7.31 + _MirrorBreak.z * 1.91, 18.93)));
                        float cellDistance = length(uv - seedPoint);
                        if (cellDistance < nearest)
                        {
                            secondNearest = nearest;
                            nearest = cellDistance;
                            nearestSeed = seedPoint;
                        }
                        else if (cellDistance < secondNearest)
                        {
                            secondNearest = cellDistance;
                        }
                    }

                    float elapsed = _MirrorBreak.x;
                    float appear = smoothstep(.02, .45, elapsed);
                    float2 pieceDirection = nearestSeed - .5;
                    pieceDirection /= max(length(pieceDirection), .06);
                    float pieceHash = Hash21(nearestSeed * 31.7 + _MirrorBreak.z);
                    float2 tangent = float2(-pieceDirection.y, pieceDirection.x);
                    float breathing = .80 + sin(elapsed * 2.35 + pieceHash * UNITY_PI * 2.0) * .22;
                    float shatter = appear * breathing;
                    float shift = (.018 + pieceHash * .035) * shatter * _MirrorBreak.y;
                    // The rift at the centre is a puncture point. Instead of merely sliding
                    // flat cells, each shard pivots and lifts away from it, as if the screen
                    // were pushed through from the other side. That keeps the five panels
                    // visibly angled throughout the first-boss fight.
                    float2 puncture = float2(.5, .5);
                    float2 panel = sampleUv - puncture;
                    float tilt = (pieceHash - .5) * .09 * shatter;
                    float tiltCos = cos(tilt);
                    float tiltSin = sin(tilt);
                    panel = float2(panel.x * tiltCos - panel.y * tiltSin,
                        panel.x * tiltSin + panel.y * tiltCos);
                    float panelLift = 1.0 + (pieceHash - .5) * .032 * shatter;
                    sampleUv = puncture + panel * panelLift;
                    sampleUv += pieceDirection * shift + tangent * shift * (pieceHash - .5) * .42;
                    float seamDistance = secondNearest - nearest;
                    mirrorCrack = (1.0 - smoothstep(.0035, .018, seamDistance)) * appear;
                    mirrorGlow = mirrorCrack * (.28 + _MirrorBreak.y * .42);
                }
                sampleUv = saturate(sampleUv);
                fixed4 scene = tex2D(_MainTex, sampleUv);
                float3 nebulaColor = lerp(_NebulaPrimary.rgb, _NebulaSecondary.rgb, cloudNoise);
                nebulaColor = lerp(nebulaColor, _NebulaSpark.rgb, _Music.w * .28);
                float3 rimNebulaColor = lerp(_NebulaSecondary.rgb, _NebulaPrimary.rgb, rimCloudNoise);
                rimNebulaColor = lerp(rimNebulaColor, _NebulaSpark.rgb, _Music.z * .22);
                float cloudGain = (1.0 + _Flight.z * .85 + jump * 2.4) * lerp(1.0, _RegionShape.y, _RegionShape.w);
                float nebulaStrength = cloudBand * (.038 + _Music.x * .11 + _WarpStrength * .052) * cloudGain;
                float rimNebulaStrength = rimCloudBand * (.028 + _Music.x * .095 + _WarpStrength * .045) * cloudGain;
                float dust = StarDust(musicUv, _TimePhase, sideDirection) * sideMask * topBottomFade;
                float ionLane = abs(sin(musicUv.x * 8.0 - musicUv.y * 6.0 + cloudNoise * 5.0 - _TimePhase * (.27 + _Music.z * .32)));
                float ionFront = (1.0 - smoothstep(.74, .97, ionLane)) * perimeterMask;
                float3 dustColor = lerp(_NebulaSecondary.rgb, _NebulaSpark.rgb, saturate(_Music.z + _Music.w * .55));
                // Central plasma halo. The two radii and the fill are kept in screen space so
                // the shape remains circular on both portrait and landscape devices.
                // Tuning points: inner=.31, outer=.47, wobble=.012, fill opacity=.032.
                float2 plasmaPoint = musicUv - .5;
                plasmaPoint.x *= aspect;
                float plasmaRadius = length(plasmaPoint);
                float plasmaAngle = atan2(plasmaPoint.y, plasmaPoint.x);
                // Sample the noise on a circle, so both sides of atan2's branch cut have
                // identical values AND tangents. The prior linear-angle noise split both rims.
                float2 plasmaCircle = float2(cos(plasmaAngle), sin(plasmaAngle));
                float plasmaWobble = (Noise(plasmaCircle * 2.4 +
                    float2(_TimePhase * .08, _Music.y * 3.0)) - .5) * .012;
                plasmaWobble += PeriodicWave(plasmaAngle, 5.0 + _Music.z * 3.0, -_TimePhase * .32) * .006;
                float plasmaInnerRadius = .31 + plasmaWobble;
                float plasmaOuterRadius = .47 + plasmaWobble * 1.15;
                float plasmaInner = exp(-Square((plasmaRadius - plasmaInnerRadius) / .014));
                float plasmaOuter = exp(-Square((plasmaRadius - plasmaOuterRadius) / .018));
                float plasmaFill = smoothstep(plasmaInnerRadius + .018, plasmaInnerRadius + .050, plasmaRadius) *
                    (1.0 - smoothstep(plasmaOuterRadius - .050, plasmaOuterRadius - .012, plasmaRadius));
                float plasmaBreath = .78 + .22 * sin(_TimePhase * .36 + cloudNoise * 2.0);
                float3 plasmaColor = lerp(_NebulaSecondary.rgb, _NebulaPrimary.rgb,
                    .5 + .5 * sin(plasmaAngle + _TimePhase * .08 + cloudNoise * 1.6));
                plasmaColor = lerp(plasmaColor, _NebulaSpark.rgb, _Music.w * .22);
                float plasmaOpacity = plasmaBreath * saturate(_Music.x * 1.65 + _Music.y * 1.25);
                scene.rgb += nebulaColor * nebulaStrength;
                scene.rgb += rimNebulaColor * rimNebulaStrength;
                scene.rgb += dustColor * dust * (.018 + _Music.w * .09 + _WarpStrength * .018);
                scene.rgb += nebulaColor * ionFront * (.006 + _Music.z * .035 + _WarpStrength * .008);
                // The old top-to-bottom lightning/plasma stroke was intentionally removed:
                // plasma now exists only as the two closed circular filaments below.
                scene.rgb += impactGlow;
                float jumpFilaments = pow(saturate(Noise(flightDirection * 31.0 +
                    float2(flightRadius * .5 - _Flight.y * .65, 7.1))), 5.0);
                float jumpEdge = smoothstep(.10, .38, flightRadius);
                scene.rgb += lerp(_NebulaPrimary.rgb, _NebulaSpark.rgb, .65) *
                    jumpFilaments * jump * jumpEdge * .85;
                // A cool, very thin crack catches the eye, but it is intentionally cosmetic:
                // no screen shake, damage, UI displacement or hit feedback is attached to it.
                scene.rgb += lerp(_NebulaSpark.rgb, float3(.78, .92, 1.0), .56) * mirrorGlow;
                scene.rgb += plasmaColor * plasmaFill * plasmaOpacity * .032;
                scene.rgb += plasmaColor * plasmaInner * plasmaOpacity * .075;
                scene.rgb += lerp(plasmaColor, _NebulaSpark.rgb, .35) * plasmaOuter * plasmaOpacity * .095;
                return scene;
            }
            ENDCG
        }
    }
    Fallback Off
}
