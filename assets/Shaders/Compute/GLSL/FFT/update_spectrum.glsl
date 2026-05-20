#[compute]
#version 450

#define PI 3.1415926535897932384626433

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(rgba32f, set = 0, binding = 0) restrict readonly uniform image2D baseSpectrum;
layout(rgba32f, set = 0, binding = 1) restrict writeonly uniform image2D spectrumTexture;

/*
how indexing fft_data works:
fft_data contains 2 2D arrays of equal size (both N*N), but 2D unsized arrays aren't possible,
so we have to do it manually
since we are running a one-dimensional FFT on each row, we index row-major

to make this easier, the macro fft_index handles everything automatically.
the arguments are the x coordinate, the y coordinate, and then which of the two arrays to access 
*/
layout(set = 1, binding = 0, std430) buffer restrict writeonly fft_buffers {
    vec2 fft_data[][2];
};
#define fft_index(x, y, b) fft_data[ x + y * texSize][ b ] 
#define fft_vindex(v, b) fft_data[ v.x + v.y * texSize][ b ] 

layout(set = 1, binding = 1, std430) buffer restrict writeonly fft_gradient_buffers {
    vec4 fft_grad_data[][2];
};

#define fft_grad_index(x, y, b) fft_grad_data[ x + y * texSize][ b ] 
#define fft_grad_vindex(v, b) fft_grad_data[ v.x + v.y * texSize][ b ] 

layout(set = 1, binding = 2, std430) buffer restrict writeonly fft_displacement_buffers {
    vec4 fft_disp_data[][2];
};

#define fft_disp_index(x, y, b) fft_disp_data[ x + y * texSize][ b ] 
#define fft_disp_vindex(v, b) fft_disp_data[ v.x + v.y * texSize][ b ] 


layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float time;       // seconds
    float tile_length; // meters
    float depth; // meters
};

//#define j vec2(0.0,1.0)
#define time_cycle 8192.0

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
    const vec2 j = vec2(0.0, 1.0);

    vec2 wave_num = vec2(gl_GlobalInvocationID.xy);
    float coeff = 2.0 * PI / tile_length;
    float halfSize = texSize / 2.0;
    vec2 f_kterm = (wave_num - vec2(halfSize, halfSize)) * coeff;
    
    vec2 Hnaught = imageLoad(baseSpectrum, id).xy;
    vec2 Hnaught_star = imageLoad(baseSpectrum, ivec2(texSize, texSize) - id).xy * vec2(1.0, -1.0);
    float k_mag = length(f_kterm);
    float dispersion = time * sqrt(9.81 * k_mag * tanh(k_mag * depth));
    vec2 exp_dispersion = exp_j(dispersion);
    vec2 H_tilde = complex_mult(Hnaught, exp_dispersion) + complex_mult(Hnaught_star, exp_dispersion * vec2(1.0, -1.0));

    fft_vindex(id, 0) = H_tilde;
    imageStore(spectrumTexture, id, vec4(H_tilde, 0.0, 1.0));
    
    vec2 ikx = complex_mult(f_kterm.x, j); 
    vec2 ikz = complex_mult(f_kterm.y, j);

    fft_grad_vindex(id, 0) = vec4(complex_mult(ikx, H_tilde), complex_mult(ikz, H_tilde));
    

    vec2 disp_coeff_x = vec2(0.0);
    vec2 disp_coeff_z = vec2(0.0);
    if (k_mag > 1e-6) {
        disp_coeff_x = -1.0 * ikx / k_mag;
        disp_coeff_z = -1.0 * ikz / k_mag;
    }

    fft_disp_vindex(id, 0) = vec4(complex_mult(disp_coeff_x, H_tilde), complex_mult(disp_coeff_z, H_tilde));
}