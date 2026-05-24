#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer readonly restrict fft_displacement_buffers {
    vec4 fft_data[][2];
};

layout(set = 0, binding = 1, std430) buffer readonly restrict fft_gradient_buffers {
    vec4 fft_grad_data[][2];
};




layout(rgba32f, set = 1, binding = 0) restrict writeonly uniform image2D heightTexture;
layout(rgba32f, set = 1, binding = 1) restrict writeonly uniform image2D gradientTexture;
layout(rgba32f, set = 1, binding = 2) restrict uniform image2D foamTexture;
layout(rgba32f, set = 1, binding = 3) restrict writeonly uniform image2D normalTexture;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float foam_grow_rate;
    float foam_decay_rate;
    float whitecap;
    uint curr_source_disp;
    uint curr_source_grad;
};

#define fft_index(x, y, b) fft_data[ x + y * texSize][ b ] 
#define fft_vindex(v, b) fft_data[ v.x + v.y * texSize][ b ] 

#define fft_grad_index(x, y, b) fft_grad_data[ x + y * texSize][ b ] 
#define fft_grad_vindex(v, b) fft_grad_data[ v.x + v.y * texSize][ b ] 

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);
    
    const float sign_shift = -2*((id.x & 1) ^ (id.y & 1)) + 1;
    const float scale = 1.0f;
    vec3 displacement = fft_vindex(id, curr_source_disp).xzw * scale;
    vec2 gradients = fft_grad_vindex(id, curr_source_grad).xy * sign_shift * scale;
    vec3 normal = normalize(vec3(-gradients.x, 1.0, -gradients.y));
    float dx_dz = fft_vindex(id, curr_source_disp).y * sign_shift;
    float dx_dx = fft_grad_vindex(id, curr_source_grad).z * sign_shift;
    float dz_dz = fft_grad_vindex(id, curr_source_grad).w * sign_shift;

    gradients = gradients / (1.0 + abs(vec2(dx_dx, dz_dz)));
    
    imageStore(heightTexture, id, vec4(displacement * sign_shift, 1.0));
    imageStore(gradientTexture, id, vec4(gradients.x, 0.0, gradients.y, 1.0) );
    imageStore(normalTexture, id, vec4(normal, 1.0));

    

    float jacobian = (dx_dx) * (dz_dz) - dx_dz * dx_dz;

    float foam_factor = -min(0.0, jacobian - whitecap);
    float foam = imageLoad(foamTexture, id).x;
    foam *= exp(-foam_decay_rate);
    foam += foam_factor * foam_grow_rate;
    foam = clamp(foam, 0.0f, 1.0f);
    imageStore(foamTexture, id, vec4(foam, foam, foam, 1.0));
}