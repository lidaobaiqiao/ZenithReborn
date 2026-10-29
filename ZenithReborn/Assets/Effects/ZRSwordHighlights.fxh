// 虚拟矩形面光的镜面近似：以反射方向和光源方向的交会确定光面。
// 不是完整面积光积分，也不能读取屏幕外的真实世界光源。
float2 SwordReflectionFace(float3 normal,float3 light,float roughness)
{
    float3 ray=reflect(float3(0,0,-1),normal);
    float3 right=normalize(float3(-light.y,light.x,.0001));
    float3 up=normalize(cross(light,right));
    float facing=dot(ray,light);
    float2 hit=float2(dot(ray,right),dot(ray,up))/max(facing,.15);
    float2 extent=float2(.38,.55)+saturate(roughness)*.18;
    float distance=max(abs(hit.x)/extent.x,abs(hit.y)/extent.y);
    float valid=smoothstep(.15,.35,facing);
    float core=(1-smoothstep(.68,1.0,distance))*valid;
    float shoulder=(1-smoothstep(.8,1.3,distance))*valid;
    return float2(core,shoulder);
}
