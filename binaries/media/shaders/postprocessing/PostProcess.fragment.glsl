#version 430 core

in vec2 UV;
#include Lighting.glsl

layout(binding = 0) uniform sampler2D texColor;
layout(binding = 1) uniform sampler2D texDepth;

uniform float LensBlurAmount;
uniform float CameraCurrentDepth;

out vec4 outColor;

const int MAX_LINES_2D = 256;

uniform int Lines2dCount;
uniform vec3 Lines2dStarts[MAX_LINES_2D];
uniform vec3 Lines2dEnds[MAX_LINES_2D];
uniform vec3 Lines2dColors[MAX_LINES_2D];

uniform vec2 resolution;


vec2 hash2x2(vec2 co) {
	return vec2(
	fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453),
	fract(sin(dot(co.yx ,vec2(12.9898,78.233))) * 43758.5453));
}

float ratio = resolution.y/resolution.x;
	
vec3 ball(vec3 colour, float sizec, float xc, float yc){
	float xdist = (abs(UV.x - xc));
	float ydist = (abs(UV.y - yc)) * ratio;
	
	//float d = (xdist * ydist);
	float d = sizec / length(vec2(xdist, ydist));
	
	return colour * (d);
}

#define MATH_E 2.7182818284

float reverseLog(float depth){
	return pow(MATH_E, depth - 1.0) / LogEnchacer;
}
float getNearDiff(float dist, float limit){
	float diff = 0.0;
	int counter = 0;
	for(float i=-2.0;i<2.0;i+=0.3){
		for(float g=-2.0;g<2.0;g+=0.3){
			float depth = abs(limit - reverseLog(texture(texDepth, UV + vec2(i*dist*ratio,g*dist)).r));
			if(depth < 0.3 && depth > 0.002) diff += 1.0;
			counter++;
		}
	}
	return diff/counter;
}
float getAveragedDepth(float dist){
	float diff = 0.0;
	int counter = 0;
	for(float i=-2.0;i<2.0;i+=0.3){
		for(float g=-2.0;g<2.0;g+=0.3){
			float depth = reverseLog(texture(texDepth, UV + vec2(i*dist*ratio,g*dist)).r);
			diff += depth;
			counter++;
		}
	}
	return diff/counter;
}
float tholdMagic(float dist, float limit){
	float diff = 0.0;
	int counter = 0;
	for(float i=-2.0;i<2.0;i+=0.3){
		for(float g=-2.0;g<2.0;g+=0.3){
			float depth = reverseLog(texture(texDepth, UV + vec2(i*dist*ratio,g*dist)).r);
			if(depth < limit && limit - depth < 0.3) diff += 1.0;
			counter++;
		}
	}
	return diff/counter;
}

	
float getSSAOAmount(float originalDepth){
	if(originalDepth > 0.999) return 0.0;
	float reversed = reverseLog(originalDepth);
	float val = (tholdMagic(reversed / 4000.0, reversed));
	if(val < 0.6) return 0.0;
	return (val - 0.6) * 0.1;
}

float getNearDiffByColor(vec3 originalColor){
	float diff = 0.0;
	for(int i=-2;i<2;i++){
		for(int g=-2;g<2;g++){
			vec3 color = texture(texColor, UV + vec2(i/400.0,g/400.0)).rgb;
			diff += distance(originalColor, color);
		}
	}
	return diff;
}

vec3 blur(float amount){
	vec3 outc = vec3(0);
	for(float g = 0; g < 14.0; g+=1.0){ 
		for(float g2 = 0; g2 < 14.0; g2+=1.0){ 
			vec2 gauss = vec2(getGaussianKernel(int(g)) * amount, getGaussianKernel(int(g2)) * amount);
			vec3 color = texture(texColor, UV + gauss).rgb;
			outc += color;
		}
	}
	return outc / (14.0*14.0);
}
vec3 blurWhitening(vec3 original){
	vec3 outc = original;
	for(float g = 0; g < 14.0; g+=2.0){ 
		for(float g2 = 0; g2 < 14.0; g2+=2.0){ 
			vec2 gauss = vec2(getGaussianKernel(int(g)), getGaussianKernel(int(g2))) * 0.2;
			vec3 color = texture(texColor, UV + gauss).rgb;
			if(color.x > 0.9 && color.y > 0.9 && color.z > 0.9){
				outc += 1.0 / (14.0*14.0);
			}
		}
	}
	return outc;
}

vec3 lensblur(float amount, float max_radius, float samples){
	vec3 finalColor = vec3(0.0,0.0,0.0);  
    float weight = 0.0;//vec4(0.,0.,0.,0.);  
    float radius = max_radius;  
	float centerDepth = texture(texDepth, UV).r;
    for(float x=samples*-1.0;x<samples;x+= 1.0) {  
        for(float y=samples*-1.0;y<samples;y+= 1.0){  
			float xc = (x / samples) * ratio;
			float yc = y / samples;
			if(length(vec2(xc, yc)) > 1.0) continue;
            vec2 coord = UV+(vec2(xc, yc) * 0.01 * amount);  
			coord.x = clamp(abs(coord.x), 0.0, 1.0);
			coord.y = clamp(abs(coord.y), 0.0, 1.0);
            if(distance(coord, UV.xy) < max_radius){  
                float depth = texture(texDepth, coord).r;
				if(centerDepth - depth < 0.1){
					vec3 texel = texture(texColor, coord).rgb;
					float w = length(texel)+0.1;  
					weight+=w;  
					finalColor += texel*w;  
				}
            }  
        }  
    } 
	return finalColor/weight;	
}
/*
vec3 line(vec2 start, vec2 end, vec3 color){
	float inter1 = (start.x - position.x) / (start.x - end.x);
	float inter2 = (start.y - position.y) / (start.y - end.y);
	vec2 ppos = mix(start, end, max(inter1, inter2));
	float anglediff = 1.0;
	if(distance(ppos, position) < 0.01) anglediff = 0.0;
	if(position.x < min(start.x, end.x)) anglediff = 1.0;
	if(position.y < min(start.y, end.y)) anglediff = 1.0;
	if(position.x > max(start.x, end.x)) anglediff = 1.0;
	if(position.y > max(start.y, end.y)) anglediff = 1.0;
	return color * (clamp(1.0 - anglediff, 0.0, 1.0));
}*/

/* Branching as boobies (line width fixed) */
float aligned(vec2 A, vec2 B, vec2 C){
	float widthK = .75/resolution.x*max(distance(A, B),max(distance(A, C), distance(B, C)));
	return 1.-smoothstep(widthK, widthK*4. ,abs((A.x * (B.y - C.y) + B.x * (C.y - A.y) + C.x * (A.y - B.y))));
}
/* Branching as boobies */
vec3 line(vec2 A, vec2 B, vec3 color){
	float dAB = distance(A, B);
	vec2 A2p = A-UV;
	vec2 B2p = B-UV;
	return color*aligned(A, B, UV)*step(distance(A, UV), dAB)*step(distance(B, UV), dAB);
}

void main()
{

	vec3 color1 = texture(texColor, UV).rgb;
	float depth = texture(texDepth, UV).r;
	
	//color1 = vec3(edge);
	if(LensBlurAmount > 0.001){
		float focus = CameraCurrentDepth;
		//float adepth = getAveragedDepth();
		float avdepth = clamp(pow(abs(depth - focus), 0.9) * 53.0 * LensBlurAmount, 0.0, 4.5 * LensBlurAmount);
		color1 = lensblur(avdepth, 0.01, 8.0);
	
	}
	
	color1 -= vec3(getSSAOAmount(depth)) / 2;
	
	color1 = blurWhitening(color1);
	
	//FXAA
	//float edge = getNearDiff(0.0001, reverseLog(depth));
	//if(edge > 0.002)color1 = blur(0.1);
	
	for(int i=0;i<Lines2dCount;i++){
		vec3 startWorld = Lines2dStarts[i];
		vec3 endWorld = Lines2dEnds[i];
		vec3 lineColor = Lines2dColors[i];
		
		vec4 startClipspace = (ProjectionMatrix * ViewMatrix) * vec4(startWorld, 1.0);
		vec2 startSSpace = ((startClipspace.xyz / startClipspace.w).xy + 1.0) / 2.0;
		
		vec4 endClipspace = (ProjectionMatrix * ViewMatrix) * vec4(endWorld, 1.0);
		vec2 endSSpace = ((endClipspace.xyz / endClipspace.w).xy + 1.0) / 2.0;
		
		color1 += line(startSSpace, endSSpace, lineColor);
	}

	for(int i=0;i<LightsCount;i++){
		vec4 clipspace = (ProjectionMatrix * ViewMatrix) * vec4(LightsPos[i], 1.0);
		vec2 sspace1 = ((clipspace.xyz / clipspace.w).xy + 1.0) / 2.0;
		if(clipspace.z < 0.0) continue;
		
		vec4 clipspace2 = (LightsPs[i] * LightsVs[i]) * vec4(CameraPosition, 1.0);
		//if(clipspace2.z < 0.0) continue;
		vec2 sspace = ((clipspace2.xyz / clipspace2.w).xy + 1.0) / 2.0;
		float dist = distance(CameraPosition, LightsPos[i]);
		dist = log(LogEnchacer*dist + 1.0) / log(LogEnchacer*LightsFarPlane[i] + 1.0);
		float overall = 0.0;
		for(float gx=-0.001;gx<0.001;gx+=0.0004){
			for(float gy=-0.001;gy<0.001;gy+=0.0004){
				float percent = lookupDepthFromLight(i, sspace + vec2(gx, gy));
				float newdist = 1.0f - (dist - percent);
				if(newdist > 1) overall += 1.0;
			}
		}
		overall /= 100;
		if(overall > 0.01) {
			color1 += ball(vec3(LightsColors[i]*2.0 * overall),0.1/ distance(CameraPosition, LightsPos[i]), sspace1.x, sspace1.y);
			color1 += ball(vec3(LightsColors[i]*2.0 * overall),2.0 / distance(CameraPosition, LightsPos[i]), sspace1.x, sspace1.y) * 0.03f;
		} else {
			//color1 += ball(vec3(dist),3.0 / distance(CameraPosition, LightsPos[i]), sspace1.x, sspace1.y);
			//color1 += ball(vec3(dist),250.0 / distance(CameraPosition, LightsPos[i]), sspace1.x, sspace1.y) * 0.1f;
		}
		// now the radial blur goes on
		/*float pxDistance = distance(UV, sspace1);
		vec2 direction = (sspace1 - UV) / pxDistance;
		vec3 colorSum = vec3(0);
		for(int g=0;g<10;g++){
			colorSum += texture(texColor, UV + (direction / 200.0) * pxDistance).rgb;
		}
		colorSum /= 10.0;
		//colorSum = colorSum.x + colorSum.y + colorSum.z > 0.9*3 ? (colorSum - 0.9) * 10.0 : vec3(0); // clip to 
		color1 += colorSum;
		//color1 = colorSum;
		*/
	}
	
	//color1 = edge > 0.01 ? vec3(1) : vec3(0);

	if(UV.x > 0.49 && UV.x < 0.51 && abs(UV.y - 0.5) < 0.0003) color1 = vec3(0);
	if(UV.y > 0.47 && UV.y < 0.53 && abs(UV.x - 0.5) < 0.0009) color1 = vec3(0);
	
	//color1 *= 1.0 - (pow(distance(UV, vec2(0.5, 0.5)) * 2.0, 2));
		
	//if(UV.x > 0.5){
	//	color1.x = log(color1.x + 1.0);
	//	color1.y = log(color1.y + 1.0);
	//	color1.z = log(color1.z + 1.0);
	//}
	
	vec3 gamma = vec3(1.0/2.2, 1.0/2.2, 1.0/2.2);
	color1 = vec3(pow(color1.r, gamma.r),
                  pow(color1.g, gamma.g),
                  pow(color1.b, gamma.b));
		
    outColor = vec4(color1, 1);
	
}