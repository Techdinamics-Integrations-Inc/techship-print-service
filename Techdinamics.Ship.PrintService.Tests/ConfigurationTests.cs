using Microsoft.Extensions.Configuration;
using Techdinamics.Ship.PrintService.Models;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;

namespace Techdinamics.Ship.PrintService.Tests;

public class ConfigurationTests
{
    [Fact]
    public void Configuration_LoadsMultiplePrintersFromJson()
    {
        // Arrange
        var json = @"{
            ""PrintService"": {
                ""Printers"": [
                    { ""Name"": ""P1"", ""Enabled"": true, ""Portal"": ""p1.io"" },
                    { ""Name"": ""P2"", ""Enabled"": false, ""Portal"": ""p2.io"" }
                ]
            }
        }";
        var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        // Act
        var serviceConfig = config.GetSection("PrintService").Get<PrintServiceConfiguration>();

        // Assert
        serviceConfig.Should().NotBeNull();
        serviceConfig!.Printers.Should().HaveCount(2);
        serviceConfig.Printers[0].Name.Should().Be("P1");
        serviceConfig.Printers[1].Enabled.Should().BeFalse();
    }

    [Fact]
    public void Configuration_LoadsMultiplePrintersFromIndexedEnvVars()
    {
        // Arrange
        var envVars = new Dictionary<string, string?>
        {
            { "PrintService:Printers:0:Name", "EnvP1" },
            { "PrintService:Printers:0:Portal", "env1.io" },
            { "PrintService:Printers:1:Name", "EnvP2" },
            { "PrintService:Printers:1:Portal", "env2.io" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(envVars)
            .Build();

        // Act
        var serviceConfig = config.GetSection("PrintService").Get<PrintServiceConfiguration>();

        // Assert
        serviceConfig.Should().NotBeNull();
        serviceConfig!.Printers.Should().HaveCount(2);
        serviceConfig.Printers[0].Name.Should().Be("EnvP1");
        serviceConfig.Printers[1].Name.Should().Be("EnvP2");
    }

    [Fact]
    public void PrinterConfiguration_EnabledDefaultsToTrue()
    {
        // Arrange & Act
        var config = new PrinterConfiguration();

        // Assert
        config.Enabled.Should().BeTrue();
    }
}
