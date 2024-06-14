// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.Protocol.Tests.Resources
{
    public class RegistrationResourceV3ProviderTests
    {
        [Fact]
        public async Task TryCreate_When_RegistrationsBaseUrl_Version_Is_Unuspported_Returns_False()
        {
            //Arrange
            var rawServiceIndex = @"{
              ""version"": ""3.0.0"",
              ""resources"": [
                {
                  ""@id"": ""https://api.nuget.org/v5/registrations-gz-semver3"",
                  ""@type"": ""RegistrationsBaseUrl/7.0.0"",
                  ""comment"": ""Fancy new semver 3 url that this client doesn't support""
                }]}";

            PackageSource packageSource = new PackageSource("https://api.nuget.org/v5/registrations-gz-semver3");
            packageSource.AllowInsecureConnections = true;
            var serviceIndexJObject = JObject.Parse(rawServiceIndex);
            var serviceIndexResource = new ServiceIndexResourceV3(serviceIndexJObject, DateTime.Now);
            var sourceRepositoryMock = new Mock<SourceRepository>();
            sourceRepositoryMock.Setup(
                mock => mock.GetResourceAsync<ServiceIndexResourceV3>(It.IsAny<CancellationToken>())).Returns(Task.FromResult(serviceIndexResource));
            sourceRepositoryMock.Setup(
                mock => mock.PackageSource).Returns(packageSource);

            var sut = new RegistrationResourceV3Provider();

            //Act
            var actual = await sut.TryCreate(sourceRepositoryMock.Object, CancellationToken.None);

            //Assert
            actual.Item1.Should().BeFalse();
            actual.Item2.Should().BeNull();
        }
    }
}
