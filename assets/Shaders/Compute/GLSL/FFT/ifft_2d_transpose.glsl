#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) buffer restrict fft_buffers {
    vec2 fft_data[][2];
};
#define fft_index(x, y, b) fft_data[ x + y * N][ b ] 
#define fft_vindex(v, b) fft_data[ v.x + v.y * N][ b ] 

layout(set = 0, binding = 1, std430) buffer restrict fft_gradient_buffers {
    vec4 fft_grad_data[][2];
};

#define fft_grad_index(x, y, b) fft_grad_data[ x + y * N][ b ] 
#define fft_grad_vindex(v, b) fft_grad_data[ v.x + v.y * N][ b ] 

layout(set = 0, binding = 2, std430) buffer restrict fft_displacement_buffers {
    vec4 fft_disp_data[][2];
};

#define fft_disp_index(x, y, b) fft_disp_data[ x + y * N][ b ] 
#define fft_disp_vindex(v, b) fft_disp_data[ v.x + v.y * N][ b ] 


layout(push_constant) restrict readonly uniform PushConstants {
    uint N;
    uint curr_source;
    uint curr_dest;
};



void main() {
    uint x_coord = gl_GlobalInvocationID.x;
    uint y_coord = gl_GlobalInvocationID.y;
    if (x_coord >= N || y_coord >= N) {
        return;
    }

    fft_index(y_coord, x_coord, curr_dest) = fft_index(x_coord, y_coord, curr_source);
    fft_grad_index(y_coord, x_coord, curr_dest) = fft_grad_index(x_coord, y_coord, curr_source);
    fft_disp_index(y_coord, x_coord, curr_dest) = fft_disp_index(x_coord, y_coord, curr_source);
}