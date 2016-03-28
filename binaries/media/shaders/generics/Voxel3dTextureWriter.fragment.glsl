#version 430 core

layout(location = 0) out vec4 outColor;

layout (binding = 6, r32ui)  uniform uimage3D VoxelsTextureRed;
layout (binding = 1, r32ui)  uniform uimage3D VoxelsTextureGreen;
layout (binding = 2, r32ui)  uniform uimage3D VoxelsTextureBlue;

layout (binding = 7, r32i)  uniform iimage3D VoxelsTextureNormalX;
layout (binding = 8, r32i)  uniform iimage3D VoxelsTextureNormalY;
layout (binding = 9, r32i)  uniform iimage3D VoxelsTextureNormalZ;

layout (binding = 3, r32ui)  uniform uimage3D VoxelsTextureCount;
uniform float BoxSize;
uniform int GridSize;

// xyz in range 0 -> 1
void WriteData3d(vec3 xyz, vec3 color, vec3 normal){
    uint r = uint(color.r * 128);
    uint g = uint(color.g * 128);
    uint b = uint(color.b * 128);
    
    int nx = int(normal.x * 128);
    int ny = int(normal.y * 128);
    int nz = int(normal.z * 128);
    
    imageAtomicAdd(VoxelsTextureRed, ivec3(xyz * float(GridSize)), r);
    imageAtomicAdd(VoxelsTextureGreen, ivec3(xyz * float(GridSize)), g);
    imageAtomicAdd(VoxelsTextureBlue, ivec3(xyz * float(GridSize)), b);
    
    imageAtomicAdd(VoxelsTextureNormalX, ivec3(xyz * float(GridSize)), nx);
    imageAtomicAdd(VoxelsTextureNormalY, ivec3(xyz * float(GridSize)), ny);
    imageAtomicAdd(VoxelsTextureNormalZ, ivec3(xyz * float(GridSize)), nz);
    
    imageAtomicAdd(VoxelsTextureCount, ivec3(xyz * float(GridSize)), 1);
    //memoryBarrier();
}

uniform int DisablePostEffects;
uniform float VDAOGlobalMultiplier;

#include LogDepth.glsl

vec2 UV = gl_FragCoord.xy / resolution.xy;


FragmentData currentFragment;

#include Lighting.glsl
#include UsefulIncludes.glsl
#include Shade.glsl
#include Direct.glsl
#include AmbientOcclusion.glsl
#include RSM.glsl
#include EnvironmentLight.glsl

#include ParallaxOcclusion.glsl



uniform vec3 LightColor;
uniform vec3 LightPosition;
uniform vec4 LightOrientation;
uniform float LightAngle;
uniform int LightUseShadowMap;
uniform int LightShadowMapType;
uniform mat4 LightVPMatrix;
uniform float LightCutOffDistance;

layout(binding = 20) uniform sampler2DShadow shadowMapSingle;

layout(binding = 21) uniform samplerCubeShadow shadowMapCube;

#define KERNEL 6
#define PCFEDGE 1
float PCFDeferred(vec2 uvi, float comparison){

    float shadow = 0.0;
    float pixSize = 1.0 / textureSize(shadowMapSingle,0).x;
    float bound = KERNEL * 0.5 - 0.5;
    bound *= PCFEDGE;
    for (float y = -bound; y <= bound; y += PCFEDGE){
        for (float x = -bound; x <= bound; x += PCFEDGE){
			vec2 uv = vec2(uvi+ vec2(x,y)* pixSize);
            shadow += texture(shadowMapSingle, vec3(uv, comparison));
        }
    }
	return shadow / (KERNEL * KERNEL);
}
vec3 ApplyLighting(FragmentData data, int samp)
{
	vec3 result = vec3(0);
    float fresnel = fresnel_again(data.normal, data.cameraPos);
    
    vec3 radiance = shade(CameraPosition, data.specularColor, data.normal, data.worldPos, LightPosition, LightColor, max(0.02, data.roughness), false);
    
    vec3 difradiance = shade(CameraPosition, data.diffuseColor, data.normal, data.worldPos, LightPosition, LightColor, 1.0, false) * (data.roughness + 1.0);
    
	if(LightUseShadowMap == 1){
		if(LightShadowMapType == 0){
			vec4 lightClipSpace = LightVPMatrix * vec4(data.worldPos, 1.0);
			if(lightClipSpace.z > 0.0){
				vec3 lightScreenSpace = (lightClipSpace.xyz / lightClipSpace.w) * 0.5 + 0.5;   

				float percent = 0;
				if(lightScreenSpace.x >= 0.0 && lightScreenSpace.x <= 1.0 && lightScreenSpace.y >= 0.0 && lightScreenSpace.y <= 1.0) {
					percent = PCFDeferred(lightScreenSpace.xy, toLogDepth2(distance(data.worldPos, LightPosition), 10000) - 0.001);
				}
				result += (radiance + difradiance) * 0.5 * percent;
                
                //subsurf
               /* float subsurfv = PCFDeferredValueSubSurf(lightScreenSpace.xy, distance(data.worldPos, LightPosition));
                
                result += subsurfv * data.diffuseColor;*/
                
			}
		
		} 
	} else if(LightUseShadowMap == 0){
		result += (radiance + difradiance) * 0.5;
	}
    result = fresnel * result;
	return result;
}


void main(){
	vec3 norm = normalize(Input.Normal);
	norm = faceforward(norm, norm, normalize(ToCameraSpace(Input.WorldPos)));
	currentFragment = FragmentData(
		DiffuseColor,
		SpecularColor,
		norm,
		normalize(Input.Tangent.xyz),
		Input.WorldPos,
		ToCameraSpace(Input.WorldPos),
		distance(CameraPosition, Input.WorldPos),
		1.0,
		Roughness,
		0.0
	);	
	
	vec2 UVx = Input.TexCoord;
	
	mat3 TBN = mat3(
		normalize(Input.Tangent.xyz),
		normalize(cross(Input.Normal, (Input.Tangent.xyz))) * Input.Tangent.w,
		normalize(Input.Normal)
	);   
	
	if(UseNormalsTex){  
		vec3 map = texture(normalsTex, UVx ).rgb;
		map = map * 2 - 1;

		map.r = - map.r;
		map.g = - map.g;
		
		currentFragment.normal = TBN * map;
	} 
	if(UseRoughnessTex) currentFragment.roughness = max(0.07, texture(roughnessTex, UVx).r);
	if(UseDiffuseTex) currentFragment.diffuseColor = texture(diffuseTex, UVx).rgb; 
	
	if(UseDiffuseTex && !UseAlphaTex)currentFragment.alpha = texture(diffuseTex, UVx).a; 
	
	if(UseSpecularTex) currentFragment.specularColor = texture(specularTex, UVx).rgb; 
	if(UseBumpTex) currentFragment.bump = texture(bumpTex, UVx).r; 
	if(UseAlphaTex) currentFragment.alpha = texture(alphaTex, UVx).r;
	
	float texdst = textureMSAAFull(normalsDistancetex, UV).a;
	//if(texdst > 0.001 && texdst < currentFragment.cameraDistance) discard;
	//if(ForwardPass == 0 && currentFragment.alpha < 0.99) discard;
	//if(ForwardPass == 1 && currentFragment.alpha > 0.99) discard;
	
	//gl_FragDepth = toLogDepth2(distance(CameraPosition, Input.WorldPos), 10000);
	
	currentFragment.normal = quat_mul_vec(ModelInfos[Input.instanceId].Rotation, currentFragment.normal);
    
    vec3 hafbox = ToCameraSpace(Input.WorldPos) / BoxSize;
    hafbox = clamp(hafbox, -1.0, 1.0);
    
    WriteData3d(hafbox * 0.5 + 0.5, currentFragment.diffuseColor * 0.04 + ApplyLighting(currentFragment, 0),  quat_mul_vec(ModelInfos[Input.instanceId].Rotation, Input.Normal));
	
	outColor = vec4(1,1,1, 0.2);
}