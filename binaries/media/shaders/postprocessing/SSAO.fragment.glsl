#version 430 core

in vec2 UV;
#include LogDepth.glsl
#include Lighting.glsl
#include UsefulIncludes.glsl
#include Shade.glsl

out vec4 outColor;

layout (std430, binding = 6) buffer RandomsBuffer
{
    float Randoms[]; 
}; 

float randomizer = 0;
void Seed(vec2 seeder){
    randomizer += 138.345341 * (rand2s(seeder)) ;
}

float randsPointer = 0;
uniform float RandomsCount;
float getRand2(){
    //if(randsPointer >= 22.0) randsPointer = 0;
    float r = rand2s(vec2(randsPointer, randsPointer*1.1234) + Time);
    randsPointer+=0.01324;
    return r;
}
vec3 random3dSample(){
    return normalize(vec3(
        getRand2() * 2 - 1, 
        getRand2() * 2 - 1, 
        getRand2() * 2 - 1
    ));
}


// using this brdf makes cosine diffuse automatically correct
vec3 BRDF(vec3 reflectdir, vec3 norm, float roughness){
    vec3 displace = random3dSample();
    displace = displace * sign(dot(norm, displace));
    return mix(displace, reflectdir, roughness);
}


mat3 TBN;
// using this brdf makes cosine diffuse automatically correct
vec3 BRDFBiased(vec3 reflectdir, vec3 norm, float roughness, vec2 huv){
    vec3 displace = TBN * hemisphereSample_cos(huv.x, huv.y);
    displace = displace * sign(dot(norm, displace));
    return mix(displace, reflectdir, roughness);
}
mat4 VP = (ProjectionMatrix * ViewMatrix);
vec2 projectOnScreen(vec3 worldcoord){
    vec4 clipspace = (VP) * vec4(worldcoord, 1.0);
    vec2 sspace1 = ((clipspace.xyz / clipspace.w).xy + 1.0) / 2.0;
    //if(clipspace.z < 0.0) return vec2(-1);
    return sspace1;
}


vec3 vec3pow(vec3 inputx, float po){
    return vec3(
    pow(inputx.x, po),
    pow(inputx.y, po),
    pow(inputx.z, po)
    );
}

vec3 lookupCubeMap(vec3 displace){
    vec3 c = texture(cubeMapTex, displace).rgb;
    return vec3pow(c, 0.2);
}


uniform int UseHBAO;
float hbao(){

    // gather data
    uint idvals = texture(meshIdTex, UV).g;
    uint tangentEncoded = texture(meshIdTex, UV).a;
    vec3 tangent = unpackSnorm4x8(tangentEncoded).xyz;
    /*
    uint packpart1 = packUnorm4x8(vec4(AORange, AOStrength, AOAngleCutoff, SubsurfaceScatteringMultiplier));
    uint packpart2 = packUnorm4x8(vec4(VDAOMultiplier, VDAOSamplingMultiplier, VDAORefreactionMultiplier, 0));
    */
    vec4 vals = unpackUnorm4x8(idvals);
    float aorange = vals.x * 2 + 0.1;
    float aostrength = vals.y * 4 + 0.1;
    float aocutoff = 1.0 - vals.z;
    
    

    vec3 posc = texture(worldPosTex, UV).rgb;
    vec3 norm = texture(normalsTex, UV).rgb;
    
    
    TBN = inverse(transpose(mat3(
        tangent,
        cross(norm, tangent),
        norm
    )));
    
    float buf = 0.0, div = 1.0/(length(posc)+1.0);
    float counter = 0.0;
    vec3 dir = normalize(reflect(posc, norm));
    float meshRoughness = 1.0 - texture(meshDataTex, UV).a;
    float samples = mix(3, 18, 1.0 - meshRoughness);
    //stepsize = 1.0 / samples;
    float ringsize = min(length(posc), 1.4);
    for(float g = 0; g < samples; g+=1)
    //for(float g = 0.0; g <= samples; g+=1)
    //for(float g2 = 0.0; g2 <= samples; g2+=1)
    {
        float minang = 0;
        
        //vec3 displace = normalize(BRDF(dir, norm, meshRoughness)) * ringsize;
        vec3 displace = normalize(BRDFBiased(dir, norm, meshRoughness, (vec2(getRand2(), getRand2())))) * ringsize;
        
        vec2 sspos2 = projectOnScreen(FromCameraSpace(posc) + displace);
        for(float g3 = 0.02; g3 < 1.0; g3+=0.1)
        {
            vec2 gauss = mix(UV, sspos2, g3*g3);
            vec3 pos = texture(worldPosTex,  gauss).rgb;
            float dt = max(0, dot(norm, normalize(pos - posc)));
            minang = max(dt * max(0, (ringsize - length(pos - posc)*0.3)/ringsize), minang);
        }
        //if(minang > aocutoff) minang = 1;
        buf += minang;
        counter+=1.0;
    }
    return pow(1.0 - (buf/counter), aostrength + meshRoughness*2);
}

uniform float AOGlobalModifier;
void main()
{   
    vec3 color1 = vec3(0);
    
    Seed(UV+1);
    randsPointer = float(randomizer * 113.86786 );
    float au = 0;
    if(UseHBAO == 1){
       // if(UV.x < 0.5) au = vec4(hbao(), 0, 0, 1);
       //else au = vec4(Radiosity(), 0, 0, 1);
       au = pow(hbao(), AOGlobalModifier);
    }
    outColor = vec4(au);
    
    
}