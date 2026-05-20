#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer readonly restrict fft_height_buffers {
    vec4 fft_data[][2];
};

layout(set = 0, binding = 1, std430) buffer readonly restrict fft_gradient_buffers {
    vec4 fft_grad_data[][2];
};

layout(set = 0, binding = 2, std430) buffer readonly restrict fft_displacement_buffers {
    vec4 fft_disp_data[][2];
};



layout(rgba32f, set = 1, binding = 0) restrict writeonly uniform image2D heightTexture;
layout(rgba32f, set = 1, binding = 1) restrict writeonly uniform image2D gradientTexture;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float time;       // seconds
    float tile_length; // meters
    float depth; // meters
    uint curr_source_height;
    uint curr_source_disp;
    uint curr_source_grad;
};

#define fft_index(x, y, b) fft_data[ x + y * texSize][ b ] 
#define fft_vindex(v, b) fft_data[ v.x + v.y * texSize][ b ] 

#define fft_grad_index(x, y, b) fft_grad_data[ x + y * texSize][ b ] 
#define fft_grad_vindex(v, b) fft_grad_data[ v.x + v.y * texSize][ b ] 

#define fft_disp_index(x, y, b) fft_disp_data[ x + y * texSize][ b ] 
#define fft_disp_vindex(v, b) fft_disp_data[ v.x + v.y * texSize][ b ] 

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);
    
    const float sign_shift = -2*((id.x & 1) ^ (id.y & 1)) + 1;
    float height = fft_vindex(id, curr_source_height).x;
    vec2 displacement = fft_disp_vindex(id, curr_source_disp).xz;
    vec2 derivs = fft_grad_vindex(id, curr_source_grad).xz;
    
    imageStore(heightTexture, id, vec4(vec3(displacement.x, height, displacement.y) * sign_shift, 1.0));
    imageStore(gradientTexture, id, vec4(derivs * sign_shift, 0.0, 1.0) );
}