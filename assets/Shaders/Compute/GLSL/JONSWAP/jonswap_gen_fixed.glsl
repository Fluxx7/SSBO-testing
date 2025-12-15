#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, std140) uniform restrict spectrumParams {
    float windSpeed19;
    vec2 windDirection;
    vec2 shapingCoefficients;
    float fetch;
    float peakFrequency;
    float peakEnhancement;
    float depth;
    float tile_length;
};

layout(rgba16f, set = 0, binding = 1) restrict readonly uniform image2D spectrumCoefficients;

layout(rgba16f, set = 1, binding = 0) restrict writeonly uniform image2D baseSpectrum;

layout(push_constant) restrict readonly uniform PushConstants {
    int texSize;
    float baseSeed;
};

vec2 dispersion_relation(in float k) {
	float a = k*depth;
	float b = tanh(a);
	float dispersion_relation = sqrt(9.81*k*b);
	float d_dispersion_relation = 0.5*9.81 * (b + a*(1.0 - b*b)) / dispersion_relation;

	return vec2(dispersion_relation, d_dispersion_relation);
}

float pierson_moskowitz(float omega, float alpha) {
    float grav_2 = 9.81 * 9.81;
    float beta = 0.74;
    float omega_naught = 9.81 / windSpeed19;
    return alpha * grav_2 * exp(-beta * pow(omega_naught / omega, 4.0)) / pow(omega, 5.0);
}

float jonswap(vec2 IDxy) {
    vec2 dk = vec2(2.0*3.14159 / tile_length);
	vec2 k_vec = (IDxy - texSize*0.5)*dk; // Wave direction
	float k = length(k_vec) + 1e-6;
	float theta = atan(k_vec.y, k_vec.x);
    vec2 dispersion = dispersion_relation(k);
    float omega = dispersion.x;

    float alpha = 0.076 * pow(windSpeed10 * windSpeed10 / (fetch * 9.81), 0.22);
    float grav_2 = 9.81 * 9.81;
    float omega_peak = (22.0 * pow(grav_2 / (windSpeed10 * fetch),1.0/3.0));
    float omega_term = (omega - omega_peak);
    float r_val = exp(-(omega_term * omega_term)/(2.0 * alpha * alpha * omega_peak * omega_peak));
    return pierson_moskowitz(omega, alpha) * pow(peakEnhancement, r_val);
}

void main() {
    if (gl_GlobalInvocationID.x >= texSize) return;
    if (gl_GlobalInvocationID.y >= texSize) return;
    ivec2 IDxy = ivec2(gl_GlobalInvocationID.xy);
    vec2 coeffs = imageLoad(spectrumCoefficients, IDxy).rg;
    vec2 coeffs_conj = imageLoad(spectrumCoefficients, texSize - IDxy).rg;
    float output_coeff = sqrt(jonswap(IDxy)) / sqrt(2.0);
    float output_conj = sqrt(jonswap(texSize-IDxy)) / sqrt(2.0);
    imageStore(baseSpectrum, IDxy, vec4(output_coeff * coeffs.x, output_coeff * coeffs.y, 0.0, 1.0));
}