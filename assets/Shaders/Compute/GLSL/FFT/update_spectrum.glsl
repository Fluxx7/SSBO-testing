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
    vec4 fft_data[][2];
};
#define fft_index(x, y, b) fft_data[ x + y * texSize][ b ] 
#define fft_vindex(v, b) fft_data[ v.x + v.y * texSize][ b ] 

layout(set = 1, binding = 1, std430) buffer restrict writeonly fft_gradient_buffers {
    vec4 fft_grad_data[][2];
};

#define fft_grad_index(x, y, b) fft_grad_data[ x + y * texSize][ b ] 
#define fft_grad_vindex(v, b) fft_grad_data[ v.x + v.y * texSize][ b ] 


layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float time;       // seconds
    float tile_length; // meters
    float depth; // meters
};

//#define j vec2(0.0,1.0)
#define time_cycle 1024.0

// (ax + j*ay) * (bx + j*by) 
// = ax*bx - ay*by + j(ay*bx + ax*by)
vec2 complex_mult(vec2 a, vec2 b) {
    return vec2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

vec2 complex_mult(float a, vec2 b) {
    return vec2(a * b.x, a * b.y );
}

vec2 complex_conj(vec2 num) {
    num.y *= -1.0;
    return num;
}

vec2 exp_j(float theta) {
    return vec2(cos(theta), sin(theta));
}


float dispersion_relation(float k_mag) {
    float omega_naught = 2.0 * PI / time_cycle;
    float omega = sqrt(9.81 * k_mag * tanh(k_mag * depth));
    return floor(omega / omega_naught) * omega_naught;
}




void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    const ivec2 id = ivec2(gl_GlobalInvocationID.xy);
    const vec2 j = vec2(0.0, 1.0);

    float coeff = 2.0 * PI / tile_length;
    const vec2 f_kterm = (id - texSize * 0.5) * coeff;
    
    const vec2 Hnaught = imageLoad(baseSpectrum, id).xy;
    const vec2 Hnaught_star = complex_conj(imageLoad(baseSpectrum, ivec2(texSize, texSize) - id).xy);
    const float k_mag = length(f_kterm);

    const vec2 exp_dispersion = exp_j(time * dispersion_relation(k_mag));
    vec2 H_tilde = complex_mult(Hnaught, exp_dispersion) + complex_mult(Hnaught_star, complex_conj(exp_dispersion));
    vec2 k_unit = vec2(0.0);
    if (k_mag > 1e-6) {
        k_unit = -f_kterm / k_mag;
    }
    // this image is only for debugging and should be removed for "production"
    imageStore(spectrumTexture, id, vec4(H_tilde, 0.0, 1.0));

    const vec2 y_disp = H_tilde;
    const vec2 jH = complex_mult(j, H_tilde);

    // since the output is guaranteed to be real,
    // multiply one of the inputs by j so that its output will be imaginary, and add it to the other input.
    // The outputs can then be read from the real and imaginary parts of the resulting complex number  
    // This cuts the number of FFTs needed in half
    const vec2 x_disp = -1.0 * k_unit.y * jH;
    const vec2 z_disp = -1.0 * k_unit.x * jH;

    const vec2 dy_dx = f_kterm.y * jH;
    const vec2 dy_dz = f_kterm.x * jH;
    const vec2 dx_dx = -H_tilde * f_kterm.y * k_unit.y;
    const vec2 dx_dz = -H_tilde * f_kterm.y * k_unit.x;
    const vec2 dz_dz = -H_tilde * f_kterm.x * k_unit.x;

    const vec2 jz_disp = complex_mult(j, z_disp);
    const vec2 jdy_dz = complex_mult(j, dy_dz);
    const vec2 jdx_dz = complex_mult(j, dx_dz);
    const vec2 jdz_dz = complex_mult(j, dz_dz);

    fft_vindex(id, 0) = vec4(H_tilde + jdx_dz,  x_disp + jz_disp);
    fft_grad_vindex(id, 0) = vec4(dy_dx + jdy_dz, dx_dx + jdz_dz);
}