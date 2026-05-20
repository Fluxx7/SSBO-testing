#[compute]
#version 450
//#define USE_NORMALIZATION

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;


layout(set = 0, binding = 0, std430) buffer restrict readonly twiddleFactors {
    vec2 twiddles[];
};

layout(set = 1, binding = 0, std430) buffer restrict fft_buffers {
    vec2 fft_data[][2];
};
#define fft_index(x, y, b) fft_data[ x + y * N][ b ] 
#define fft_vindex(v, b) fft_data[ v.x + v.y * N][ b ] 


layout(set = 1, binding = 1, std430) buffer restrict fft_gradient_buffers {
    vec4 fft_grad_data[][2];
};

#define fft_grad_index(x, y, b) fft_grad_data[ x + y * N][ b ] 
#define fft_grad_vindex(v, b) fft_grad_data[ v.x + v.y * N][ b ] 

layout(set = 1, binding = 2, std430) buffer restrict fft_displacement_buffers {
    vec4 fft_disp_data[][2];
};

#define fft_disp_index(x, y, b) fft_disp_data[ x + y * N][ b ] 
#define fft_disp_vindex(v, b) fft_disp_data[ v.x + v.y * N][ b ] 


layout(push_constant) restrict readonly uniform PushConstants {
    uint N;
    uint stage;
    uint curr_source;
    uint curr_dest;
};

// (ax + j*ay) * (bx + j*by) 
// = ax*bx - ay*by + j(ay*bx + ax*by)
vec2 complex_mult(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

vec2 complex_mult(float a, vec2 b) {
    return vec2(a * b.x, a * b.y );
}

vec2 complex_mult(vec2 a, float b) {
    return vec2(a.x * b, a.y * b );
}

#define dest(index) fft_index( index , y_coord, curr_dest)
#define source(index) fft_index( index , y_coord, curr_source)

void main() {
    uint x_coord = gl_GlobalInvocationID.x;
    uint y_coord = gl_GlobalInvocationID.y;
    if (x_coord >= N/2 || y_coord >= N) {
        return;
    }

    uint index_base = N >> (stage + 1);
    uint group_size = 1 << stage;
    uint group = x_coord & ~(group_size - 1);
    uint group_index = x_coord & (group_size - 1);
    
    // compute the indices to read from
    uint source_index_1 = x_coord;
    uint source_index_2 = source_index_1 + N/2;

    // compute the indices to write to
    uint dest_index_1 = x_coord + group; 
    uint dest_index_2 = dest_index_1 + group_size;

    // calculate the twiddle factor index to read from and retrieve that value 
    uint twiddle_index = (source_index_1 & (group_size - 1)) * index_base;
    vec2 twiddle = twiddles[twiddle_index];

    vec2 twiddled_x = vec2(0);
    vec2 twiddled_y = vec2(0);
    vec2 twiddled_z = vec2(0);
    vec2 even_x = vec2(0);
    vec2 even_y = vec2(0);
    vec2 even_z = vec2(0);


    // load and twiddle the "odd" value, and load the "even" value
    twiddled_y = complex_mult(twiddle, source(source_index_2));
    even_y = source(source_index_1);

    // calculate the output values and write them to the determined indices
#ifdef USE_NORMALIZATION
    dest(dest_index_2) = complex_mult(even_y - twiddled_y, 0.5);
    dest(dest_index_1) = complex_mult(even_y + twiddled_y, 0.5);
#else
    dest(dest_index_2) = even_y - twiddled_y;
    dest(dest_index_1) = even_y + twiddled_y;
#endif
    twiddled_x = complex_mult(twiddle, fft_disp_index(source_index_2, y_coord, curr_source).xy);
    even_x = fft_disp_index(source_index_1, y_coord, curr_source).xy;
    twiddled_z = complex_mult(twiddle, fft_disp_index(source_index_2, y_coord, curr_source).zw);
    even_z = fft_disp_index(source_index_1, y_coord, curr_source).zw;

    // calculate the output values and write them to the determined indices
#ifdef USE_NORMALIZATION
    fft_disp_index(dest_index_2, y_coord, curr_dest) = vec4(even_x - twiddled_x, even_z - twiddled_z) * 0.5;
    fft_disp_index(dest_index_1, y_coord, curr_dest) = vec4(even_x + twiddled_x, even_z + twiddled_z) * 0.5;
#else
    fft_disp_index(dest_index_2, y_coord, curr_dest) = vec4(even_x - twiddled_x, even_z - twiddled_z);
    fft_disp_index(dest_index_1, y_coord, curr_dest) = vec4(even_x + twiddled_x, even_z + twiddled_z);
#endif
    
    twiddled_x = complex_mult(twiddle, fft_grad_index(source_index_2, y_coord, curr_source).xy);
    even_x = fft_grad_index(source_index_1, y_coord, curr_source).xy;
    twiddled_z = complex_mult(twiddle, fft_grad_index(source_index_2, y_coord, curr_source).zw);
    even_z = fft_grad_index(source_index_1, y_coord, curr_source).zw;

    // calculate the output values and write them to the determined indices
#ifdef USE_NORMALIZATION
    fft_grad_index(dest_index_2, y_coord, curr_dest) = vec4(even_x - twiddled_x, even_z - twiddled_z) * 0.5;
    fft_grad_index(dest_index_1, y_coord, curr_dest) = vec4(even_x + twiddled_x, even_z + twiddled_z) * 0.5;
#else
    fft_grad_index(dest_index_2, y_coord, curr_dest) = vec4(even_x - twiddled_x, even_z - twiddled_z);
    fft_grad_index(dest_index_1, y_coord, curr_dest) = vec4(even_x + twiddled_x, even_z + twiddled_z);
#endif
       
    
}
