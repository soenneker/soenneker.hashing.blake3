using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Hashing.Blake3.Abstract;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.File.Registrars;
using Soenneker.Utils.MemoryStream.Registrars;

namespace Soenneker.Hashing.Blake3.Registrars;

/// <summary>
/// Registration of <see cref="IBlake3Util"/> and <see cref="Blake3Util"/> with the DI container.
/// </summary>
public static class Blake3UtilRegistrar
{
    /// <summary>
    /// Registers <see cref="IBlake3Util"/> and <see cref="Blake3Util"/> as scoped.
    /// Requires <see cref="Soenneker.Utils.File.Abstract.IFileUtil"/> and <see cref="Soenneker.Utils.Directory.Abstract.IDirectoryUtil"/> to be registered.
    /// </summary>
    public static IServiceCollection AddBlake3UtilAsScoped(this IServiceCollection services)
    {
        services.AddMemoryStreamUtilAsScoped().AddFileUtilAsScoped()
                .AddDirectoryUtilAsScoped();
        
        services.TryAddScoped<IBlake3Util, Blake3Util>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="IBlake3Util"/> and <see cref="Blake3Util"/> as singleton.
    /// Requires <see cref="Soenneker.Utils.File.Abstract.IFileUtil"/> and <see cref="Soenneker.Utils.Directory.Abstract.IDirectoryUtil"/> to be registered.
    /// </summary>
    public static IServiceCollection AddBlake3UtilAsSingleton(this IServiceCollection services)
    {
        services.AddMemoryStreamUtilAsSingleton().AddFileUtilAsSingleton()
                .AddDirectoryUtilAsSingleton();
        
        services.TryAddSingleton<IBlake3Util, Blake3Util>();
        return services;
    }
}
