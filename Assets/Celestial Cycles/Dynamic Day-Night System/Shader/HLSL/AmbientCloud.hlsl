// Clouds
float2 SphereIntersect(float sphereRadius, float cosChi, float radialDistance, float rcpRadialDistance)
{
    float d = Sq(sphereRadius * rcpRadialDistance) - saturate(1 - cosChi * cosChi);
    return (d < 0) ? d : (radialDistance * float2(-cosChi - sqrt(d), -cosChi + sqrt(d)));
}

float2 SphereIntersect(float sphereRadius, float cosChi, float radialDistance)
{
    return SphereIntersect(sphereRadius, cosChi, radialDistance, rcp(radialDistance));
}

float3 GetCloudVolumeIntersection(int index, float3 dir)
{
    const float earthRadius = 6378100.0;
    return dir * -SphereIntersect(2000.0 + earthRadius, -dir.y, earthRadius).x;
}

float3 DegToOrientation(float deg)
{
    float rad = 2 * PI * deg / 360.0;
    return float3(cos(rad), sin(rad), 0.0);
}

float2 GetLatLongCoords(float3 dir, float upperHemisphereOnly)
{
    const float2 invAtan = float2(0.1591, 0.3183);
    float2 uv = float2(atan2(dir.x, dir.z), asin(dir.y)) * invAtan + 0.5;
    uv.y = upperHemisphereOnly ? uv.y * 2.0 - 1.0 : uv.y;
    return uv;
}

float PhaseHG(float g, float cosTheta)
{
    float g2 = g * g;
    float nom = 1.0 - g2;
    float denom = 4.0 * PI * pow(1.0 + g2 - 2.0 * g * (cosTheta), 1.5);
    return nom / denom;
}

float3 CalculateCloudSterngth(float4 color, float4 sterngth)
{
    color.r = color.r * sterngth.r;
    color.g = color.g * sterngth.g;
    color.b = color.b * sterngth.b;
    color.a = color.a * sterngth.a;
    float3 result = float3(color.r, color.r, color.r);
    result += float3(color.g, color.g, color.g);
    result += float3(color.b, color.b, color.b);
    result += float3(color.a, color.a, color.a);
    return result;
}

float4 CalculateClouds(Texture2D cloudMap, SamplerState ss, float3 positionWS, float3 worldCameraPos, float direction, float speed, float altitude, float4 strength, float3 skyColor, float density)
{
#if SHADERGRAPH_PREVIEW
    float3 lightDir = float3(0.5, 0.5, 0);
    float3 lightCol = 1;
#else
    Light mainLight = GetMainLight();
    float3 lightDir = normalize(mainLight.direction);
    float3 lightCol = mainLight.color;
#endif

    float3 view = normalize(worldCameraPos.xyz - positionWS.xyz);
    float3 panoPosition = GetCloudVolumeIntersection(0, -view);
    float scrollDist = 2 * altitude;
    float2 panoAlpha = frac(speed * _Time.y * 10 / scrollDist + float2(0.0, 0.5)) - 0.5;
    float3 scrollDirection = DegToOrientation(direction);
    float3 delta = float3(scrollDirection.x, 0.0, scrollDirection.y);
    float3 dir = normalize(panoPosition + panoAlpha.x * delta * scrollDist);
    float2 uvx = GetLatLongCoords(dir, 1.0);
    dir = normalize(panoPosition + panoAlpha.y * delta * scrollDist);
    float2 uvy = GetLatLongCoords(dir, 1.0);

    float4 colorX = SAMPLE_TEXTURE2D(cloudMap, ss, uvx);
    float4 colorY = SAMPLE_TEXTURE2D(cloudMap, ss, uvy);

    float3 viewDir = normalize(worldCameraPos.xyz - positionWS.xyz);

    float hgPhase = PhaseHG(0.8, dot(lightDir, viewDir));
    float3 color = lerp(CalculateCloudSterngth(colorX, strength), CalculateCloudSterngth(colorY, strength), abs(2.0 * panoAlpha.x));
    float3 shade = pow(-0.5 * color, 2);

    float3 weights = lightDir > 0 ? color : shade;
    float3 sqrDir = lightDir * lightDir;
    float transmission = dot(sqrDir, weights);

    float3 directDiffuse = exp(-1.0 * (1 - transmission) * density) * lightCol;
    float3 rimEnhance = min(1, pow(transmission, 6)) * hgPhase * lightCol * 10;
    directDiffuse += rimEnhance;
    directDiffuse.rgb = saturate(lerp(directDiffuse.rgb * 0.1, directDiffuse.rgb, saturate(length(skyColor))));

    float opacity = pow(color.y, 1.2);
    opacity = viewDir.y < 0 ? opacity : 0;
    opacity = saturate(opacity * normalize(positionWS).y);

    return float4(directDiffuse.rgb, opacity);
}

void Clouds_float(Texture2D cloudMap, SamplerState ss, float3 positionWS, float3 worldCameraPos, float direction, float speed, float altitude, float4 strength, float3 skyColor, float density, out float3 diffuse, out float opacity)
{
    float4 result = CalculateClouds(cloudMap, ss, positionWS, worldCameraPos, direction, speed, altitude, strength, skyColor, density);
    diffuse = result.xyz;
    opacity = result.w;
}