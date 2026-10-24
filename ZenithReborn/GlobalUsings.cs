// tModLoader 自带编译器不读 csproj 的 ImplicitUsings，缺 System 系列命名空间。
// 这里用全局 using 补齐，效果等同 ImplicitUsings=enable，且对 dotnet build 无副作用。
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
