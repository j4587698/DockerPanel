using System.Text.Json.Serialization;

namespace DockerPanel.API.Models;

/// <summary>
/// Docker网络详细信息原始JSON模型
/// </summary>
public class DockerNetworkInspect
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Created")]
    public string Created { get; set; } = string.Empty;

    [JsonPropertyName("Scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("Driver")]
    public string Driver { get; set; } = string.Empty;

    [JsonPropertyName("EnableIPv4")]
    public bool EnableIPv4 { get; set; }

    [JsonPropertyName("EnableIPv6")]
    public bool EnableIPv6 { get; set; }

    [JsonPropertyName("IPAM")]
    public DockerNetworkIpam IPAM { get; set; } = new();

    [JsonPropertyName("Internal")]
    public bool Internal { get; set; }

    [JsonPropertyName("Attachable")]
    public bool Attachable { get; set; }

    [JsonPropertyName("Ingress")]
    public bool Ingress { get; set; }

    [JsonPropertyName("ConfigFrom")]
    public DockerNetworkConfigFrom ConfigFrom { get; set; } = new();

    [JsonPropertyName("ConfigOnly")]
    public bool ConfigOnly { get; set; }

    [JsonPropertyName("Containers")]
    public Dictionary<string, DockerNetworkContainerInfo>? Containers { get; set; }

    [JsonPropertyName("Options")]
    public Dictionary<string, string> Options { get; set; } = new();

    [JsonPropertyName("Labels")]
    public Dictionary<string, string> Labels { get; set; } = new();
}

/// <summary>
/// Docker网络IPAM配置
/// </summary>
public class DockerNetworkIpam
{
    [JsonPropertyName("Driver")]
    public string Driver { get; set; } = string.Empty;

    [JsonPropertyName("Options")]
    public Dictionary<string, string>? Options { get; set; }

    [JsonPropertyName("Config")]
    public List<DockerNetworkIpamConfigEntry> Config { get; set; } = new();
}

/// <summary>
/// Docker网络IPAM配置条目
/// </summary>
public class DockerNetworkIpamConfigEntry
{
    [JsonPropertyName("Subnet")]
    public string Subnet { get; set; } = string.Empty;

    [JsonPropertyName("IPRange")]
    public string? IPRange { get; set; }

    [JsonPropertyName("Gateway")]
    public string? Gateway { get; set; }

    [JsonPropertyName("AuxAddress")]
    public Dictionary<string, string>? AuxAddress { get; set; }
}

/// <summary>
/// Docker网络容器信息
/// </summary>
public class DockerNetworkContainerInfo
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("EndpointID")]
    public string EndpointID { get; set; } = string.Empty;

    [JsonPropertyName("MacAddress")]
    public string MacAddress { get; set; } = string.Empty;

    [JsonPropertyName("IPv4Address")]
    public string IPv4Address { get; set; } = string.Empty;

    [JsonPropertyName("IPv6Address")]
    public string IPv6Address { get; set; } = string.Empty;
}

/// <summary>
/// Docker网络配置来源
/// </summary>
public class DockerNetworkConfigFrom
{
    [JsonPropertyName("Network")]
    public string Network { get; set; } = string.Empty;
}