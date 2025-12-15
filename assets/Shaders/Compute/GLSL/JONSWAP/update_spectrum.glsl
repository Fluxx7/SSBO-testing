#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 0) restrict readonly uniform image2D baseSpectrum;

layout(rgba16f, set = 1, binding = 0) restrict writeonly uniform image2D currSpectrum;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float time;       // seconds
    float tile_length; // meters
    float depth; // meters
};

//#define j vec2(0.0,1.0)
#define time_cycle 1024.0

// (ax + j*ay) * (bx + j*by) 
// = ax*bx - ay*by + j(ay*bx + ax*by)
vec2 complex_mult(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

vec2 complex_mult(float a, vec2 b) {
    return vec2(a * b.x, a * b.y );
}

vec2 exp_j(float theta) {
    return vec2(cos(theta), sin(theta));
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    vec2 wave_num = vec2(gl_GlobalInvocationID.xy);
    float coeff = 2.0 * PI / tile_length;
    float halfSize = texSize / 2.0;
    vec2 f_kterm = (wave_num - vec2(halfSize, halfSize)) * coeff;
    
    vec2 Hnaught = imageLoad(baseSpectrum, id).xy;
    vec2 Hnaught_star = imageLoad(baseSpectrum, ivec2(texSize, texSize) - id).xy * vec2(1.0, -1.0);
    float k_mag = length(f_kterm);
    float dispersion = time * sqrt(9.81 * k_mag * tanh(k_mag * depth));
    vec2 exp_dispersion = exp_j(dispersion);
    vec2 H_tilde = complex_mult(Hnaught, exp_dispersion) + complex_mult(Hnaught_star, exp_dispersion * vec2(1.0, -1.0));


    imageStore(currSpectrum, id, vec4(H_tilde, 0.0, 1.0));
}