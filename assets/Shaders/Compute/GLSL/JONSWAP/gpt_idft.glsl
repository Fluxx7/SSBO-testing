#[compute]
#version 450
#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 0) restrict readonly uniform image2D baseSpectrum;

layout(rgba16f, set = 1, binding = 0) restrict writeonly uniform image2D heightTexture;
layout(rgba16f, set = 1, binding = 1) restrict writeonly uniform image2D gradientTexture;
layout(rgba16f, set = 1, binding = 2) restrict writeonly uniform image2D normalTexture;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;      // N
    float time;       // simulation time (seconds)
    float tile_length; // L (meters)
    float depth;      // water depth (meters), use large for deep water
};

vec2 complex_mult(in vec2 a, in vec2 b) {
    // (a.x + j a.y) * (b.x + j b.y)
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

vec2 exp_j(float theta) {
    return vec2(cos(theta), sin(theta));
}

// dispersion for finite depth: returns omega
float dispersion_omega(float k, float depth) {
    // handle k==0
    if (k <= 1e-8) return 0.0;
    float g = 9.81;
    float kd = k * depth;
    float tanh_kd = tanh(kd);
    return sqrt(g * k * tanh_kd);
}

void main() {
    uint gx = gl_GlobalInvocationID.x;
    uint gy = gl_GlobalInvocationID.y;
    int N = texSize;
    //if (gx >= uint(N) || gy >= uint(N)) return;

    // index in pixel/grid
    ivec2 pix = ivec2(gx, gy);

    // prepare accumulators
    float eta_acc = 0.0;
    vec2 grad_acc = vec2(0.0);

    // physical constants for mapping index -> k and index -> x
    float L = tile_length;
    float dx = L / float(N);              // physical spacing in x
    float dk = 2.0 * PI / L;              // wavevector step
    float halfN = float(N) * 0.5;

    // compute physical sample position x in meters for current pixel
    vec2 x_pos = (vec2(pix) / float(N)) * L;

    // precompute imaginary unit for complex multiplication
    const vec2 I = vec2(0.0, 1.0);

    // loop over k-space: n,m in [-N/2 .. N/2-1]
    for (int n=-N/2; n<N/2; n++) {
      for (int m=-N/2; m<N/2; m++) {
        ivec2 texCoord = ivec2(n + N/2, m + N/2);
        vec2 Hk = imageLoad(baseSpectrum, texCoord).rg;

        vec2 k_vec = vec2(float(n), float(m)) * (2.0*PI / L);
        float k_len = length(k_vec);
        float omega = sqrt(9.81 * k_len); // deep water

        vec2 x_pos = (vec2(gx, gy) / float(N)) * L;
        float phase = dot(k_vec, x_pos) + omega * time;

        vec2 contrib = complex_mult(Hk, exp_j(phase));
        eta_acc += contrib.x;
      }
    }
    


    // normalization for inverse DFT:
    float inv_norm = 1.0 / (float(N*N));
    float eta = eta_acc * inv_norm;
    vec2 gradient = grad_acc * inv_norm;

    // store results (you can change which channels contain what for debug)
    imageStore(heightTexture, pix, vec4(0.0, eta, 0.0, 1.0));
    // gradient.x = dη/dx, gradient.y = dη/dy (world units)
    imageStore(gradientTexture, pix, vec4(gradient.xy, 0.0, 1.0));

    // normal (approx): N = normalize( (-dη/dx, 1, -dη/dy) )
    vec3 n = normalize(vec3(-gradient.x, 1.0, -gradient.y));
    imageStore(normalTexture, pix, vec4(n, 1.0));
}
