
//mat4 HBAO_VP = (ProjectionMatrix * ViewMatrix);

vec2 HBAO_projectOnScreen(vec3 worldcoord){
    vec4 clipspace = (VPMatrix) * vec4(worldcoord, 1.0);
    vec2 sspace1 = ((clipspace.xyz / clipspace.w).xy )* 0.5 + 0.5;
    return sspace1;
}

float VeryFastAO(
	
	vec3 position,
	vec3 normal,
	float roughness
	
){
	float dcenter = reverseLog(texture(depthTex, UV).r);
   // vec3 posc = ToCameraSpace(position);
	float div = (1.0 / (length(dcenter)+1)) * 0.25;
	float dim = 0.0;
	#define fastao_samples 226
	#define fastao_samples_inv 1.0/fastao_samples
	#define pidiv (PI*2)/fastao_samples
	for(int i = 0; i < fastao_samples; i++){
		float r1 = i*pidiv;
		float r2 = pow(getRand(), 2.0);
        vec2 zx = UV + vec2(sin(r1), cos(r1)) * r2 * div;
		float d = reverseLog(texture(depthTex, zx).r);
		float x = clamp(dcenter - d, 0.0, 0.6);
		dim += (smoothstep(0.6, 0.0, x)) * x * 14;
	}
	return smoothstep(0.0, 1.0, 1.0 - dim * fastao_samples_inv);
}
float insideBox(vec2 v, vec2 bottomLeft, vec2 topRight) {
    vec2 s = step(bottomLeft, v) - step(topRight, v);
    return s.x * s.y;   
}

float AmbientOcclusionSingle(
	
	vec3 position,
	vec3 normal,
	float roughness,
	float hemisphereSize
	
){
	vec2 pixelSize = vec2(length(dFdx(position)), length(dFdy(position)));
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
    const float samples = 41.0;
    const float stepsize = PI*2 / samples;
    float ringsize = clamp(length(pixelSize)*hemisphereSize*500, 0.3, 2.0);
	vec2 uv = HBAO_projectOnScreen(position);
	roughness = 1.0 - roughness;
    for(float g = 0.0; g < PI*2; g+=stepsize)
    {
		float rd = rand(UV + g + roughness + ringsize);
        vec2 zx = vec2(sin(g), cos(g)) * rd;
		
        vec3 displace = mix((TBN * normalize(vec3(zx, sqrt(1.0 - length(zx))))), dir, roughness) * ringsize;
				
		vec2 gauss = mix(uv, HBAO_projectOnScreen(position + displace), rd);
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
	float ao = AmbientOcclusionSingle(position, normal, roughness, 0.3);
	//float ao = VeryFastAO(position, normal, roughness);
    ao = ao + AmbientOcclusionSingle(position, normal, roughness, 0.125);
	ao *= 0.5;
	//ao *= AmbientOcclusionSingle(position, normal, tangent, roughness, 0.35);
	//ao = AmbientOcclusionSingle(position, normal, tangent, roughness);
	//if(metalness < 1.0) ao = AmbientOcclusionSingle(position, normal, tangent, 1.0)) * 0.2;
	return ao;// * 0.3333;
}