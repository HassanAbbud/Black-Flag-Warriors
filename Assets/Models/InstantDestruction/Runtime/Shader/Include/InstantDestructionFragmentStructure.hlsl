#ifndef INSTANT_DESTRUCTION_INCLUDED
#define INSTANT_DESTRUCTION_INCLUDED

// 1. Structure definition: Add parameters required for explosion
struct FragmentVertex {
    float3 position;    // Current vertex coordinates
    float3 normal;      // Normal
    float2 uv;          // UV coordinates
    float3 center;      // Triangle centroid (axis for rotation and scattering)
    float randomId;     // Per-fragment random value (used for rotation speed/direction variance)
    float3 velocity;    // For movement vector
    float3 rotationaxis;// Rotation axis
    float3 impactforce; // Vector representing the direction and magnitude of the impact force
};

// 2. Buffer declaration
// * Referenced by this name from both Compute Shader and Shader Graph
StructuredBuffer<FragmentVertex> _FragmentBuffer;

// 3. For Shader Graph Custom Function
// Receives VertexID and returns coordinates and attributes calculated in CS
void GetFragmentData_float(float vertexID,
                            out float3 position, out float3 normal, out float2 uv, out float3 center,
                            out float randomId, out float3 velocity, out float3 rotationaxis, out float3 impactforce) {
    // Get data using vertex ID
    FragmentVertex data = _FragmentBuffer[(uint)vertexID];

    position = data.position;
    normal   = data.normal;
    uv       = data.uv;
    center   = data.center;
    randomId = data.randomId;
    velocity = data.velocity;
    rotationaxis = data.rotationaxis;
    impactforce = data.impactforce;
}

#endif