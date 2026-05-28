#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

#define PI 3.1415926535897932384626433
#define g 9.81

layout(rgba32f, set = 0, binding = 0) restrict writeonly uniform image2D baseSpectrum;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float tile_length;
    float A;
    float B;
    float depth;
};

#define water_surface_tension 0.074
#define water_density 1000.0

float dispersion_relation(float k_mag, out float tan_k) {
    tan_k = tanh(k_mag * depth + pow(k_mag, 3.0) * water_surface_tension / water_density);
    return sqrt(g * k_mag * tan_k);
}

float dphi_dk(float k_mag, float omega, float tan_k) {
    if (omega < 1e-6 || k_mag < 1e-6) {
        return 0.0;
    }
    float sech2 = 1.0 - tan_k * tan_k;
    float dfdk  = depth + 3.0 * k_mag * k_mag * water_surface_tension / water_density;
    float domega_dk = (g * tan_k + g * k_mag * sech2 * dfdk) / (2.0 * omega);
    return domega_dk / k_mag;
}

float a_b_spectrum(float omega) {
    float omegaRcp = 0.0;
    if (omega > 1e-6) {
        omegaRcp = 1.0 / omega;
    }
    float first_term = A * pow(omegaRcp, 5.0);
    float second_term = exp(-B * pow(omegaRcp, 4.0));
   
    return first_term * second_term;
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    vec2 k_vec = 2.0 * PI * (id - texSize * 0.5) / tile_length;
    float k_mag = length(k_vec);
    float tan_k = 0.0;
    float omega = dispersion_relation(k_mag, tan_k);
    float k_rcp = 0.0;
    if (k_mag > 1e-6) {
        k_rcp = 1.0 / k_mag;
    }
    imageStore(baseSpectrum, id, vec4(a_b_spectrum(omega) * dphi_dk(k_mag, omega, tan_k) * k_rcp, 0.0, 0.0, 1.0));
}