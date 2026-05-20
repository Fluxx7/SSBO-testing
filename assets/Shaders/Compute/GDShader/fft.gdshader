shader_type compute;

#define PI 3.1415926535897932384626433

uniform image2D in baseSpectrum : format(rgba16f);
uniform image2D spectrumTexture : format(rgba16f);
uniform image2D out heightTexture : format(rgba16f);
uniform image2D out gradientTexture : format(rgba16f);

buffer restrict {
    vec2 twiddles[];
} : (twiddleFactors, std430);

buffer out restrict {
    vec2 fft_data[][2];
} : (fft_buffers, std430);

#define fft_index(x, y, b) fft_data[ x + y * texSize][ b ] 
#define fft_vindex(v, b) fft_data[ v.x + v.y * texSize][ b ] 

buffer out restrict {
    vec4 fft_grad_data[][2];
} : (fft_gradient_buffers, std430);

#define fft_grad_index(x, y, b) fft_grad_data[ x + y * texSize][ b ] 
#define fft_grad_vindex(v, b) fft_grad_data[ v.x + v.y * texSize][ b ] 

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

void PrepTwiddles(int N) : kernel(16, 16, 1) {
    if (GlobalInvocationID.x >= (N / 2)) return;
    uint x = GlobalInvocationID.x;
    float theta = 2.0 * PI * float(x) / float(N);
    twiddles[x] = vec2(cos(theta), sin(theta));
}

void UpdateSpectrum(int texSize, float time, float tile_length, float depth) : kernel(16, 16, 1) {
    if (GlobalInvocationID.x >= texSize) return;
    if (GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(GlobalInvocationID.xy);
    const vec2 j = vec2(0.0, 1.0);

    vec2 wave_num = vec2(GlobalInvocationID.xy);
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
}

#define dest(index) fft_index( index , y_coord, curr_dest)
#define source(index) fft_index( index , y_coord, curr_source)

void EvalStage(uint N, uint stage, uint curr_source, uint curr_dest) : kernel(16,16,1) {
    uint x_coord = GlobalInvocationID.x;
    uint y_coord = GlobalInvocationID.y;
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

    // #############################
    // ###### DISPLACEMENT #########
    // #############################

    // load and twiddle the "odd" value, and load the "even" value
    vec2 twiddled_odd = complex_mult(twiddle, source(source_index_2));
    vec2 even = source(source_index_1);

    // calculate the output values and write them to the determined indices
    dest(dest_index_2) = even - twiddled_odd;
    dest(dest_index_1) = even + twiddled_odd;
    
    

    // ##########################
    // ###### GRADIENTS #########
    // ##########################

    vec2 twiddled_grad_x = complex_mult(twiddle, fft_grad_index(source_index_2, y_coord, curr_source).xy);
    vec2 even_grad_x = fft_grad_index(source_index_1, y_coord, curr_source).xy;
    vec2 twiddled_grad_z = complex_mult(twiddle, fft_grad_index(source_index_2, y_coord, curr_source).zw);
    vec2 even_grad_z = fft_grad_index(source_index_1, y_coord, curr_source).zw;

    // calculate the output values and write them to the determined indices
    fft_grad_index(dest_index_2, y_coord, curr_dest) = vec4(even_grad_x - twiddled_grad_x, even_grad_z - twiddled_grad_z);
    fft_grad_index(dest_index_1, y_coord, curr_dest) = vec4(even_grad_x + twiddled_grad_x, even_grad_z + twiddled_grad_z);
}


void Transpose(uint N, uint curr_source, uint curr_dest) : kernel(16, 16, 1) {
    uint x_coord = GlobalInvocationID.x;
    uint y_coord = GlobalInvocationID.y;
    if (x_coord >= N || y_coord >= N) {
        return;
    }

    fft_index(y_coord, x_coord, curr_dest) = fft_index(x_coord, y_coord, curr_source);
    fft_grad_index(y_coord, x_coord, curr_dest) = fft_grad_index(x_coord, y_coord, curr_source);
}


void Unpack(int texSize, float time, float tile_length, float depth, uint curr_source) : kernel(16, 16, 1) {
    if (GlobalInvocationID.x >= texSize) return;
    if (GlobalInvocationID.y >= texSize) return;
    ivec2 id = ivec2(GlobalInvocationID.xy);
    
    const float sign_shift = -2*((id.x & 1) ^ (id.y & 1)) + 1;
    float height = fft_vindex(id, curr_source).x;
    vec2 derivs = fft_grad_vindex(id, curr_source).xz;
    
    imageStore(heightTexture, id, vec4(vec3(0.0, height, 0.0) * sign_shift, 1.0));
    imageStore(gradientTexture, id, vec4(derivs * sign_shift, 0.0, 1.0) );
}