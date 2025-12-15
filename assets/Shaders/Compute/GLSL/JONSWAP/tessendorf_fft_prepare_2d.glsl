#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 0) restrict readonly uniform image2D butterflyFactors;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
};

#define j vec2(0.0,1.0)

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

vec2 h_tilde(ivec2 kterm, float k_mag) {
    int halfsize = texSize/2;
    vec2 Hnaught = imageLoad(baseSpectrum, kterm + halfsize).xy;
    vec2 Hnaught_star = imageLoad(baseSpectrum, -kterm + halfsize).xy;
    Hnaught_star.y *= -1.0;
    
    
    float w_naught = 2.0 * PI / time_cycle;
    float dispersion = time * sqrt(9.81 * k_mag * tanh(k_mag * depth));

    vec2 H_out = Hnaught * exp_j(dispersion);
    vec2 Hstar_out = Hnaught_star * exp_j(-dispersion);


    return H_out + Hstar_out;
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);
    float sum = 0.0;
    vec2 displacement = vec2(0.0);
    vec2 derivatives = vec2(0.0);
    vec2 xterm = vec2(id) * tile_length / float(texSize);
    float tex_sq = float(texSize * texSize);

    int halfsize = texSize/2;
    for (int n = -halfsize; n < halfsize; n++) {
        for (int m = -halfsize; m < halfsize; m++) {
            vec2 f_kterm = vec2(n, m) * 2.0 * PI / tile_length;
            ivec2 kterm = ivec2(n,m);
            float k_mag = length(f_kterm) + 1e-6;
            
            
            vec2 exp_term = exp_j(dot(f_kterm, xterm));
            vec2 summand = complex_mult( h_tilde(kterm, k_mag), exp_term);
            sum += summand.x;
            vec2 derivative = vec2(complex_mult(j * f_kterm.x, summand).x, complex_mult(j * f_kterm.y, summand).x);
            derivatives += derivative;
            displacement += -derivative / k_mag;
        }
    }
    //derivatives /= tex_sq;
    //displacement /= tex_sq;
    
    //sum /= tex_sq;
    
    imageStore(heightTexture, id, vec4(displacement.x, sum, displacement.y, 1.0));
    imageStore(gradientTexture, id, vec4(derivatives, 0.0, 1.0));
}