using System;
using Newtonsoft.Json;
using Xunit;

namespace GreyHackTerminalUI.Tests.Browser
{
    public class TerminalStateSerializationTests
    {
        [Fact]
        public void Serialize_TerminalPID_UsesCamelCase()
        {
            var state = new TerminalState { TerminalPID = 12345 };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"terminalPID\"", json);
            Assert.DoesNotContain("\"TerminalPID\"", json);
        }

        [Fact]
        public void Serialize_IsWaitingForInput_UsesCamelCase()
        {
            var state = new TerminalState { IsWaitingForInput = true };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"isWaitingForInput\"", json);
            Assert.DoesNotContain("\"IsWaitingForInput\"", json);
        }

        [Fact]
        public void Serialize_IsWaitingForAnyKey_UsesCamelCase()
        {
            var state = new TerminalState { IsWaitingForAnyKey = true };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"isWaitingForAnyKey\"", json);
            Assert.DoesNotContain("\"IsWaitingForAnyKey\"", json);
        }

        [Fact]
        public void Serialize_LastInputPrompt_UsesCamelCase()
        {
            var state = new TerminalState { LastInputPrompt = "Enter your name:" };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"lastInputPrompt\"", json);
            Assert.DoesNotContain("\"LastInputPrompt\"", json);
        }

        [Fact]
        public void Serialize_BrowserIsLoading_UsesCamelCase()
        {
            var state = new TerminalState { BrowserIsLoading = true };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"browserIsLoading\"", json);
            Assert.DoesNotContain("\"BrowserIsLoading\"", json);
        }

        [Fact]
        public void Serialize_BrowserIsReady_UsesCamelCase()
        {
            var state = new TerminalState { BrowserIsReady = true };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"browserIsReady\"", json);
            Assert.DoesNotContain("\"BrowserIsReady\"", json);
        }

        [Fact]
        public void Serialize_BrowserTitle_UsesCamelCase()
        {
            var state = new TerminalState { BrowserTitle = "Test Page" };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"browserTitle\"", json);
            Assert.DoesNotContain("\"BrowserTitle\"", json);
        }

        [Fact]
        public void Serialize_ConsoleErrorCount_UsesCamelCase()
        {
            var state = new TerminalState { ConsoleErrorCount = 3 };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"consoleErrorCount\"", json);
            Assert.DoesNotContain("\"ConsoleErrorCount\"", json);
        }

        [Fact]
        public void Serialize_LastConsoleError_UsesCamelCase()
        {
            var state = new TerminalState { LastConsoleError = "TypeError: undefined" };
            var json = JsonConvert.SerializeObject(state);
            
            Assert.Contains("\"lastConsoleError\"", json);
            Assert.DoesNotContain("\"LastConsoleError\"", json);
        }

        [Fact]
        public void Serialize_FullState_AllPropertiesCamelCase()
        {
            var state = new TerminalState
            {
                TerminalPID = 999,
                IsWaitingForInput = true,
                IsWaitingForAnyKey = false,
                LastInputPrompt = "Password:",
                BrowserIsLoading = false,
                BrowserIsReady = true,
                BrowserTitle = "Login Page",
                ConsoleErrorCount = 0,
                LastConsoleError = null
            };
            
            var json = JsonConvert.SerializeObject(state);
            
            // Should NOT contain any PascalCase property names
            Assert.DoesNotContain("\"TerminalPID\"", json);
            Assert.DoesNotContain("\"IsWaitingForInput\"", json);
            Assert.DoesNotContain("\"IsWaitingForAnyKey\"", json);
            Assert.DoesNotContain("\"LastInputPrompt\"", json);
            Assert.DoesNotContain("\"BrowserIsLoading\"", json);
            Assert.DoesNotContain("\"BrowserIsReady\"", json);
            Assert.DoesNotContain("\"BrowserTitle\"", json);
            Assert.DoesNotContain("\"ConsoleErrorCount\"", json);
            Assert.DoesNotContain("\"LastConsoleError\"", json);
            
            // Should contain all camelCase property names
            Assert.Contains("\"terminalPID\"", json);
            Assert.Contains("\"isWaitingForInput\"", json);
            Assert.Contains("\"isWaitingForAnyKey\"", json);
            Assert.Contains("\"lastInputPrompt\"", json);
            Assert.Contains("\"browserIsLoading\"", json);
            Assert.Contains("\"browserIsReady\"", json);
            Assert.Contains("\"browserTitle\"", json);
            Assert.Contains("\"consoleErrorCount\"", json);
            Assert.Contains("\"lastConsoleError\"", json);
        }

        [Fact]
        public void Deserialize_CamelCaseJson_PopulatesProperties()
        {
            var json = @"{
                ""terminalPID"": 42,
                ""isWaitingForInput"": true,
                ""isWaitingForAnyKey"": false,
                ""lastInputPrompt"": ""Enter code:"",
                ""browserIsLoading"": true,
                ""browserIsReady"": false,
                ""browserTitle"": ""Loading..."",
                ""consoleErrorCount"": 2,
                ""lastConsoleError"": ""Network error""
            }";
            
            var state = JsonConvert.DeserializeObject<TerminalState>(json);
            
            Assert.NotNull(state);
            Assert.Equal(42, state.TerminalPID);
            Assert.True(state.IsWaitingForInput);
            Assert.False(state.IsWaitingForAnyKey);
            Assert.Equal("Enter code:", state.LastInputPrompt);
            Assert.True(state.BrowserIsLoading);
            Assert.False(state.BrowserIsReady);
            Assert.Equal("Loading...", state.BrowserTitle);
            Assert.Equal(2, state.ConsoleErrorCount);
            Assert.Equal("Network error", state.LastConsoleError);
        }

        [Fact]
        public void Serialize_NullStrings_SerializesAsNull()
        {
            var state = new TerminalState
            {
                LastInputPrompt = null,
                BrowserTitle = null,
                LastConsoleError = null
            };
            
            var json = JsonConvert.SerializeObject(state);
            
            // Null values should serialize as null, not be omitted
            Assert.Contains("\"lastInputPrompt\":null", json);
            Assert.Contains("\"browserTitle\":null", json);
            Assert.Contains("\"lastConsoleError\":null", json);
        }

        [Fact]
        public void Serialize_SpecialCharactersInStrings_Escapes()
        {
            var state = new TerminalState
            {
                LastInputPrompt = "Enter \"name\": ",
                BrowserTitle = "<script>alert('xss')</script>",
                LastConsoleError = "Line 1\nLine 2\tTabbed"
            };
            
            var json = JsonConvert.SerializeObject(state);
            
            // JSON should properly escape special characters
            Assert.Contains("\\\"name\\\"", json);
            Assert.Contains("\\n", json);
            Assert.Contains("\\t", json);
        }

        [Fact]
        public void RoundTrip_SerializeDeserialize_PreservesValues()
        {
            var original = new TerminalState
            {
                TerminalPID = 8675309,
                IsWaitingForInput = true,
                IsWaitingForAnyKey = false,
                LastInputPrompt = "Enter password:",
                BrowserIsLoading = false,
                BrowserIsReady = true,
                BrowserTitle = "Secure Login",
                ConsoleErrorCount = 1,
                LastConsoleError = "Warning: deprecated API"
            };
            
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<TerminalState>(json);
            
            Assert.NotNull(restored);
            Assert.Equal(original.TerminalPID, restored.TerminalPID);
            Assert.Equal(original.IsWaitingForInput, restored.IsWaitingForInput);
            Assert.Equal(original.IsWaitingForAnyKey, restored.IsWaitingForAnyKey);
            Assert.Equal(original.LastInputPrompt, restored.LastInputPrompt);
            Assert.Equal(original.BrowserIsLoading, restored.BrowserIsLoading);
            Assert.Equal(original.BrowserIsReady, restored.BrowserIsReady);
            Assert.Equal(original.BrowserTitle, restored.BrowserTitle);
            Assert.Equal(original.ConsoleErrorCount, restored.ConsoleErrorCount);
            Assert.Equal(original.LastConsoleError, restored.LastConsoleError);
        }
    }

    public class TerminalState
    {
        [JsonProperty("terminalPID")]
        public int TerminalPID { get; set; }
        
        [JsonProperty("isWaitingForInput")]
        public bool IsWaitingForInput { get; set; }
        
        [JsonProperty("isWaitingForAnyKey")]
        public bool IsWaitingForAnyKey { get; set; }
        
        [JsonProperty("lastInputPrompt")]
        public string LastInputPrompt { get; set; }
        
        [JsonProperty("browserIsLoading")]
        public bool BrowserIsLoading { get; set; }
        
        [JsonProperty("browserIsReady")]
        public bool BrowserIsReady { get; set; }
        
        [JsonProperty("browserTitle")]
        public string BrowserTitle { get; set; }
        
        [JsonProperty("consoleErrorCount")]
        public int ConsoleErrorCount { get; set; }
        
        [JsonProperty("lastConsoleError")]
        public string LastConsoleError { get; set; }
    }
}
