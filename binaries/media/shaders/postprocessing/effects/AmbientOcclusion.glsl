vec2 HBAO_projectOnScreen(vec3 worldcoord){
    vec4 clipspace = (VPMatrix) * vec4(worldcoord, 1.0);
    vec2 sspace1 = ((clipspace.xyz / clipspace.w).xy )* 0.5 + 0.5;
    return sspace1;
}

float AmbientOcclusionSingle(
vec3 position,
vec3 normal,
float roughness,
float hemisphereSize
){
    //vec2 pixelSize = vec2(length(dFdx(position)), length(dFdy(position)));
    vec3 posc = ToCameraSpace(position);
    vec3 vdir = normalize(posc);
    vec3 tangent = getTangentPlane(normal);
    //normal = normalize(cross((dFdx(position) - position), (dFdy(position) - position))); 
    
    mat3 TBN = inverse(transpose(mat3(
    tangent,
    cross(normal, tangent),
    normal
    )));
    
    float buf = 0.0;
    vec3 dir = normalize(reflect(posc, normal));
    float samples = mix(6, 32, roughness);
    float stepsize = PI*2 / samples;
    float ringsize = min(length(posc), hemisphereSize);
    vec2 uv = HBAO_projectOnScreen(position);
    roughness = 1.0 - roughness;
	float rd = rand2s(UV);
    for(float g = 0.0; g < PI*2; g+=stepsize)
    {
        rd = (rd+g*12.75753);
        vec3 zx = vec3(sin(g), cos(g), rd);
        
        vec3 displace = mix(TBN * normalize(zx), dir, roughness) * ringsize;
        
        vec2 gauss = mix(uv, HBAO_projectOnScreen(position + displace), fract(rd));
        if(gauss.x < 0.0 || gauss.x > 1.0 || gauss.y < 0.0 || gauss.y > 1.0) continue;
        vec3 pos = reconstructCameraSpace(gauss);
        float dt = max(0, dot(normal, normalize(pos - posc)));
        
        buf += dt * ((ringsize - min(length(pos - posc), ringsize))/ringsize);
    }
    return clamp(pow(1.0 - (buf/samples), 6), 0.0, 1.0);
}

float AmbientOcclusion(

vec3 position,
vec3 normal,
float roughness,
float metalness

){
    float ao = AmbientOcclusionSingle(position, normal, roughness, 1.1);
    //float ao = VeryFastAO(position, normal, roughness);
    //ao = ao + AmbientOcclusionSingle(position, normal, roughness, 0.525);
   // ao *= 0.5;
    //ao *= AmbientOcclusionSingle(position, normal, tangent, roughness, 0.35);
    //ao = AmbientOcclusionSingle(position, normal, tangent, roughness);
    //if(metalness < 1.0) ao = AmbientOcclusionSingle(position, normal, tangent, 1.0)) * 0.2;
    return ao;// * 0.3333;
}