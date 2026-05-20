#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 0) restrict readonly uniform image2D baseSpectrum;

layout(rgba16f, set = 1, binding = 0) restrict writeonly uniform image2D heightTexture;
layout(rgba16f, set = 1, binding = 1) restrict writeonly uniform image2D gradientTexture;

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
    float sum = 0.0;
    vec2 displacement = vec2(0.0);
    const vec2 j = vec2(0.0, 1.0);
    float deriv_x = 0.0;
    float deriv_z = 0.0;
    vec2 xterm = vec2(id) * tile_length / float(texSize);
    float tex_sq = float(texSize * texSize);

    int halfsize = texSize/2;
    for (int n = -halfsize; n < halfsize; n++) {
        for (int m = -halfsize; m < halfsize; m++) {
            vec2 f_kterm = vec2(n, m) * 2.0 * PI / tile_length;
            ivec2 kterm = ivec2(n,m);
            float k_mag = length(f_kterm);
            float k_mag_rcp = 1.0;
            if (k_mag > 1e-6) {
                k_mag_rcp = 1.0/k_mag;
            }

            vec2 h_tilde = imageLoad(baseSpectrum, kterm + halfsize).xy;
            vec2 ih = h_tilde.yx * vec2(-1.0, 1.0);
            
            
            vec2 exp_term = exp_j(dot(f_kterm, xterm));
            vec2 summand = complex_mult( h_tilde, exp_term);
            sum += summand.x;
            vec2 deriv_term_x = complex_mult(f_kterm.x, ih);
            vec2 deriv_term_z = complex_mult(f_kterm.y, ih);
            vec2 dhdx = complex_mult(deriv_term_x, exp_term);
            vec2 dhdz = complex_mult(deriv_term_z, exp_term);
            deriv_x += dhdx.x;
            deriv_z += dhdz.x;
            displacement += -vec2(dhdx.x, dhdz.x) * k_mag_rcp;
        }
    }
    // displacement /= texSize;
    deriv_x /= texSize;
    deriv_z /= texSize;
    sum /= texSize;

    imageStore(heightTexture, id, vec4(0.0, sum, 0.0, 1.0));
    //imageStore(heightTexture, id, vec4(displacement.x, sum, displacement.y, 1.0));
    imageStore(gradientTexture, id, vec4(deriv_x, deriv_z, 0.0, 1.0));
}