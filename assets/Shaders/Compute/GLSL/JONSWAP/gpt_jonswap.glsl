#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, std140) uniform restrict jonswapParams {
    float windSpeed10;
    float windSpeed19;
    vec2 windDirection;
    float fetch;
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

// --- helper: canonical dispersion (omega and dω/dk) ---
vec2 dispersion_relation(in float k) {
    // returns (omega, domega_dk)
    float kd = k * depth;
    float tanh_kd = tanh(kd);
    float omega = sqrt(9.81 * k * tanh_kd);
    // derivative dω/dk = 0.5 * g * (tanh(kd) + k*d/dk[tanh(kd)]) / ω
    // d/dk[tanh(kd)] = depth * (1 - tanh^2(kd))
    float d_tanh = depth * (1.0 - tanh_kd * tanh_kd);
    float domega_dk = 0.5 * 9.81 * (tanh_kd + k * d_tanh) / omega;
    return vec2(omega, domega_dk);
}

// --- directional spreading (cos^s) ---
// windDir: unit vector (x,y) pointing *towards* wind propagation (i.e. wave travel direction)
float directional_spread(float theta, vec2 windDir, float exponent) {
    // compute angle between k and windDir
    float windAngle = atan(windDir.y, windDir.x);
    float dtheta = theta - windAngle;
    // wrap to [-PI, PI]
    if (dtheta > 3.14159265) dtheta -= 2.0*3.14159265;
    if (dtheta < -3.14159265) dtheta += 2.0*3.14159265;
    float c = cos(dtheta);
    c = max(0.0, c); // no negative values
    // simple (unnormalized) cos^s spreading; you may optionally normalize across angles
    return pow(c, exponent);
}

// --- Pierson-Moskowitz base spectrum (canonical form) ---
float pierson_moskowitz_omega(float omega, float alpha_pm, float omega_p) {
    // S_pm(ω) = α * g^2 / ω^5 * exp(-5/4 * (ω_p / ω)^4)
    float g = 9.81;
    float term = exp(-1.25 * pow(omega_p / max(omega, 1e-6), 4.0));
    return alpha_pm * (g*g) * term / pow(max(omega, 1e-6), 5.0);
}

// --- refined JONSWAP returns S_omega (spectral density in frequency space) ---
float jonswap_Somega(float omega, float U10, float fetch_m, float gamma) {
    // Empirical peak frequency (fetch-limited). Use a standard empirical formula:
    // omega_p ≈ 22.0 * (g^2 / (U10 * fetch))^(1/3)  (this is an often-used empirical constant)
    float g = 9.81;
    float omega_p = 22.0 * pow((g*g) / (U10 * fetch_m + 1e-9), 1.0/3.0);

    // PM alpha — can be tuned or computed from U10/fetch (keep typical ~0.0081 or fetch-based)
    // Here we compute a fetch-based alpha per common parameterizations:
    float alpha_pm = 0.0081; // safe default; you can supply a better empirical formula

    float S_pm = pierson_moskowitz_omega(omega, alpha_pm, omega_p);

    // Peak shape: normalized gaussian around omega_p
    // width parameter sigma: use 0.07 (left) / 0.09 (right) in freq - here we use a symmetric
    float sigma = (omega <= omega_p) ? 0.07 : 0.09;
    float r = exp(- pow((omega - omega_p) / (sigma * omega_p), 2.0) / 2.0);

    // JONSWAP: S(ω) = S_pm(ω) * γ^r
    return S_pm * pow(gamma, r);
}

void main() {
    if (gl_GlobalInvocationID.x >= uint(texSize) || gl_GlobalInvocationID.y >= uint(texSize)) return;
    ivec2 IDi = ivec2(gl_GlobalInvocationID.xy);
    vec2 uv_i = (vec2(IDi) - vec2(texSize) * 0.5) ; // centered index
    float dk = 2.0 * 3.14159265358979323846 / tile_length; // Δk
    vec2 k_vec = uv_i * dk; // units: 1/m
    float k = length(k_vec);

    // load spectrumCoefficients (assumed to provide random phase or direction multipliers)
    vec2 coeffs = imageLoad(spectrumCoefficients, IDi).rg;

    if (k < 1e-6) {
        // DC / very small k: set zero amplitude
        imageStore(baseSpectrum, IDi, vec4(0.0));
        return;
    }

    // dispersion & derivative
    vec2 disp = dispersion_relation(k);
    float omega = disp.x;
    float domega_dk = disp.y;

    // compute S_omega (JONSWAP) using windSpeed10/fetch & peakEnhancement
    float S_omega = jonswap_Somega(omega, windSpeed10, fetch, peakEnhancement);

    // convert to S_k (per wavenumber): S_k = S_omega * dω/dk
    float S_k = S_omega * domega_dk;

    // directional spreading: simple cos^s. If you already encode direction in spectrumCoefficients, skip this
    vec2 windDirUnit = normalize(windDirection);
    float theta = atan(k_vec.y, k_vec.x); // correct order
    float dirExponent = 12.0; // tuneable (8..30)
    float D = directional_spread(theta, windDirUnit, dirExponent);

    // (optional) normalize directional spreading so that angle integral = 1.
    // For performance, one may skip exact normalization and just tune exponent empirically.
    S_k *= D;

    // area per k-bin
    float area_bin = dk * dk;

    // amplitude per complex spectral bin (split between +k and -k) -> sqrt(0.5 * S_k * area_bin)
    float amplitude = sqrt( max(0.0, 0.5 * S_k * area_bin) );

    // apply any coefficient map (coeffs.x/coeffs.y could be random-phase cos/sin)
    // we assume spectrumCoefficients stores (cosφ, sinφ) or direction modifiers
    vec2 complex = amplitude * coeffs; // coeffs should be unit-length (cosφ,sinφ)
    // write RG = complex (real, imag). B/A channels can be used for conjugate packing if needed.
    imageStore(baseSpectrum, IDi, vec4(complex.xy, 0.0, 1.0));
}
