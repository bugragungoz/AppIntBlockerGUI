using AppIntBlockerGUI.Core;
using AppIntBlockerGUI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;

namespace AppIntBlockerGUI.Tests
{
    [TestClass]
    public class FirewallServiceTests
    {
        private Mock<ILoggingService> _mockLoggingService;
        private Mock<Func<IPowerShellWrapper>> _mockWrapperFactory;
        private Mock<IPowerShellWrapper> _mockPowerShellWrapper;
        private FirewallService _firewallService;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockLoggingService = new Mock<ILoggingService>();
            _mockWrapperFactory = new Mock<Func<IPowerShellWrapper>>();
            _mockPowerShellWrapper = new Mock<IPowerShellWrapper>();

            // When the factory is called, it returns our mocked wrapper instance.
            _mockWrapperFactory.Setup(f => f()).Returns(_mockPowerShellWrapper.Object);

            _firewallService = new FirewallService(_mockLoggingService.Object, _mockWrapperFactory.Object);
        }

        [TestMethod]
        public async Task GetExistingRulesAsync_WhenRulesExist_ReturnsRuleNames()
        {
            // Arrange
            var expectedRules = new[] { "AppBlocker Rule - Rule1", "AppBlocker Rule - Rule2" };
            var psObjects = new Collection<PSObject>();
            foreach (var rule in expectedRules)
            {
                var psObject = new PSObject();
                psObject.Properties.Add(new PSNoteProperty("DisplayName", rule));
                psObjects.Add(psObject);
            }

            _mockPowerShellWrapper.Setup(p => p.InvokeAsync()).ReturnsAsync(psObjects);
            _mockPowerShellWrapper.SetupGet(p => p.HadErrors).Returns(false);

            // Act
            var result = await _firewallService.GetExistingRulesAsync(_mockLoggingService.Object, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
            CollectionAssert.AreEqual(expectedRules, result.ToList());
            _mockLoggingService.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            _mockPowerShellWrapper.Verify(p => p.AddScript(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task GetExistingRulesAsync_WhenPowerShellHasErrors_LogsErrorsAndReturnsEmptyList()
        {
            // Arrange
            _mockPowerShellWrapper.SetupGet(p => p.HadErrors).Returns(true);
            var errorRecord = new ErrorRecord(new Exception("Test PS Error"), "TestErrorId", ErrorCategory.NotSpecified, null);
            _mockPowerShellWrapper.SetupGet(p => p.Errors).Returns(new Collection<ErrorRecord> { errorRecord });
            _mockPowerShellWrapper.Setup(p => p.InvokeAsync()).ReturnsAsync(new Collection<PSObject>());

            // Act
            var result = await _firewallService.GetExistingRulesAsync(_mockLoggingService.Object, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
            _mockLoggingService.Verify(l => l.LogError(It.Is<string>(s => s.Contains("PowerShell error")), It.IsAny<Exception>()), Times.Once);
        }
    }
} 