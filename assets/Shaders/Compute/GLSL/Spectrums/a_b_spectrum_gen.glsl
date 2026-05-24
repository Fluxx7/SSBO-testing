#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

#define PI 3.1415926535897932384626433

layout(rgba32f, set = 0, binding = 0) restrict writeonly uniform image2D baseSpectrum;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float tile_length;
    float A;
    float B;
};

float a_b_spectrum(vec2 k_vec) {
    float k = length(k_vec);
    float kRcp = 1.0;
    
     if (k > 1e-6) {
        kRcp = 1.0/k;
    }

    float first_term = A * pow(kRcp, 5.0);
    float second_term = exp(-B * pow(kRcp, 4.0));

   
    return first_term * second_term;
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    vec2 k_vec = 2.0 * PI * (id - texSize * 0.5) / tile_length;
    imageStore(baseSpectrum, id, vec4(a_b_spectrum(k_vec), 0.0, 0.0, 1.0));
}