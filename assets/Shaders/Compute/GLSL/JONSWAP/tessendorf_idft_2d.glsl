#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 0) restrict readonly uniform image2D baseSpectrum;

layout(rgba16f, set = 1, binding = 0) restrict writeonly uniform image2D heightTexture;
layout(rgba16f, set = 1, binding = 1) restrict writeonly uniform image2D gradientTexture;
layout(rgba16f, set = 1, binding = 2) restrict writeonly uniform image2D normalTexture;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float time;       // seconds
    float tile_length; // meters
    float depth; // meters
};

#define j vec2(0.0,1.0)
#define time_cycle 1024.0
// (a.x + j*a.y) * (b.x + j*b.y) = a.x * b.x + b.x * j * a.y + b.y * j * a.x - b.y * a.y = a.x * b.x - b.y * a.y + j * (a.y * b.x + a.x * b.y)
vec2 complex_mult(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

vec2 exp_j(float theta) {
    return vec2(cos(theta), sin(theta));
}

vec2 h_tilde(ivec2 k) {
    vec2 Fuv = imageLoad(baseSpectrum, k + texSize/2).rg;
    vec2 Fuv_star = imageLoad(baseSpectrum, k + texSize/2).ba;
   
    float w = sqrt(length(k) * 9.81);
    float w_naught = 2.0 * PI / time_cycle;
    float dispersion_relation = floor(w/w_naught) * w_naught;

    vec2 Fuv_out = complex_mult(Fuv, exp_j(dispersion_relation * time));
    vec2 Fuv_star_out = complex_mult(Fuv_star, exp_j(-dispersion_relation * time));

    return Fuv_out + Fuv_star_out;
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 uv = ivec2(gl_GlobalInvocationID.xy);
    int N = texSize;
    float M = float(texSize);

    float L = tile_length;
    float dx = L / M;
    float dk = 2.0 * PI / L;

    vec2 xy = vec2(uv) * dx;

    float real_sum = 0.0;
    vec2 derivatives = vec2(0.0);

    for (int n = -N/2; n < N/2; n++) {
        for (int m = -N/2; m < N/2; m++) {
            vec2 f_kterm = vec2(n, m) * dk;
            ivec2 kterm = ivec2(f_kterm);
            
            // h(k, t) * e^(jk•x)
            vec2 freq_sample = complex_mult(h_tilde(kterm), exp_j(dot(f_kterm,xy)));

            real_sum += freq_sample.x;
            derivatives += complex_mult(j * f_kterm, freq_sample);
        }
    }
    float n_sq = M * M;
    float displacement = real_sum / n_sq;
    derivatives /= n_sq;
   
    imageStore(heightTexture, uv, vec4(0.0, real_sum, 0.0, 1.0));
    imageStore(gradientTexture, uv, vec4(derivatives.x, derivatives.y, 0.0, 1.0));
    imageStore(normalTexture, uv, vec4(normalize(vec3(-derivatives.x, 1.0, -derivatives.y)), 1.0));
}