#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 1, local_size_z = 1) in;

// texture of frequency domain values
// red is real amplitude, green is complex amplitude
// direction from center is the direction of the wave, distance is frequency
layout(set = 0, binding = 0) restrict readonly uniform image2D freqDomain;

layout(rgba16f, set = 0, binding = 1) writeonly uniform image2D timeDomain;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    ivec2 center;
};

void main() {
    int i = gl_GlobalInvocationID.x;
    float sum = 0.0;
    
    // discrete fourier transform
    // output to one pixel
    for (int j = 0; j < texSize; j++) {
        ivec2 tex_coord = ivec2((i - center.x), (j - center.y));
        vec2 direction = normalize(tex_coord);
        float frequency = length(tex_coord);
        vec4 curr_texel = texelFetch(freqDomain, ivec2(i, j), 0);
        float amplitude_real = curr_texel.r;
        float amplitude_complex = curr_texel.g;
        sum += out;
    }
    ijsum *= 1.0 / (texSize * texSize);
    imageStore(timeDomain, coord, vec4(sum, 0.0, 0.0, 0.0)); 
    
}