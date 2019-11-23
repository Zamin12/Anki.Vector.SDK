﻿// <copyright file="Authentication.cs" company="Wayne Venables">
//     Copyright (c) 2019 Wayne Venables. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Anki.Vector.Exceptions;
using Anki.Vector.ExternalInterface;
using Grpc.Core;
using Newtonsoft.Json.Linq;
using Zeroconf;
using static Anki.Vector.ExternalInterface.ExternalInterface;

namespace Anki.Vector
{
    /// <summary>
    /// This static class for logging into Vector and creating <see cref="RobotConfiguration"/> instances for connecting to a Vector robot.
    /// <para>The <see cref="Login(string, string, string, string, IPAddress)"/> method is the best way to use this class; provide all the necessary parameters and that method
    /// will retrieve the certificate, login to Anki's server, login to Vector, and return a <see cref="RobotConfiguration"/> instance that be stored and used to connect to Vector.</para>
    /// <para>The remaining methods in this class can be used to retrieve each piece of connection information separately</para>
    /// </summary>
    public static class Authentication
    {
        /// <summary>
        /// The anki application key
        /// </summary>
        private const string AnkiAppKey = "aung2ieCho3aiph7Een3Ei";

        /// <summary>
        /// The shared HTTP client
        /// </summary>
        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>
        /// Performs a complete login to the root and returns a filled in <see cref="RobotConfiguration"/> instance.
        /// </summary>
        /// <param name="serialNumber">The robot serial number.</param>
        /// <param name="robotName">Name of the robot.</param>
        /// <param name="emailAddress">The user's username.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="ipAddress">The optional robot IP address.</param>
        /// <returns>A task that represents the asynchronous operation; the task result contains the new robot configuration.</returns>
        /// <exception cref="System.ArgumentException">
        /// Serial number must be provided - serialNumber
        /// or
        /// Robot name must be provided - robotName
        /// or
        /// User name must be provided - username
        /// or
        /// Password must be provided - password
        /// or
        /// IP address could not be determined; please provide IP address. - ipAddress
        /// </exception>
        public static async Task<RobotConfiguration> Login(string serialNumber, string robotName, string emailAddress, string password, IPAddress ipAddress = null)
        {
            robotName = StandardizeRobotName(robotName);
            serialNumber = serialNumber?.ToLower();
            ipAddress = await FindRobotAddress(robotName) ?? ipAddress;

            var result = new RobotConfiguration
            {
                RobotName = robotName,
                IPAddress = ipAddress ?? throw new VectorAuthenticationException(VectorAuthenticationFailureType.IPAddress, "IP address could not be determined; please provide IP address."),
                Certificate = await GetCertificate(serialNumber),
                SerialNumber = serialNumber
            };
            result.Guid = await GetTokenGuid(await GetSessionToken(emailAddress, password), result.Certificate, robotName, result.IPAddress);
            return result;
        }

        /// <summary>
        /// Updates the specified robot configuration with a new login
        /// </summary>
        /// <param name="robotConfiguration">The robot configuration.</param>
        /// <param name="emailAddress">The email address.</param>
        /// <param name="password">The password.</param>
        /// <param name="ipAddress">The IP address.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="VectorAuthenticationException">IP address could not be determined; please provide IP address.</exception>
        public static async Task Login(RobotConfiguration robotConfiguration, string emailAddress, string password, IPAddress ipAddress = null)
        {
            ipAddress = await FindRobotAddress(robotConfiguration.RobotName) ?? ipAddress;
            robotConfiguration.IPAddress = ipAddress ?? throw new VectorAuthenticationException(VectorAuthenticationFailureType.IPAddress, "IP address could not be determined; please provide IP address.");
            if (string.IsNullOrEmpty(robotConfiguration.Certificate)) robotConfiguration.Certificate = await GetCertificate(robotConfiguration.SerialNumber);
            robotConfiguration.Guid = await GetTokenGuid(await GetSessionToken(emailAddress, password), robotConfiguration.Certificate, robotConfiguration.RobotName, robotConfiguration.IPAddress);
        }

        /// <summary>
        /// Gets the certificate for the specific robot by serial number.
        /// </summary>
        /// <param name="serialNumber">The serial number.</param>
        /// <returns>A task that represents the asynchronous operation; the task result contains the certificate.</returns>
        /// <exception cref="Anki.Vector.Exceptions.VectorAuthenticationException">
        /// Serial number must be provided
        /// or
        /// Serial number is not in the correct format.
        /// or
        /// Serial number is invalid.
        /// or
        /// </exception>
        public static async Task<string> GetCertificate(string serialNumber)
        {
            // Serial number must be provided
            if (string.IsNullOrEmpty(serialNumber)) throw new ArgumentException("Serial number must be provided.", nameof(serialNumber));
            if (!SerialNumberIsValid(serialNumber)) throw new ArgumentException("Serial number is not in the correct format.", nameof(serialNumber));

            // Get the certificate from the web service
            using (var response = await HttpClient.GetAsync($"https://session-certs.token.global.anki-services.com/vic/{serialNumber}"))
            {
                // If the result is forbidden then the serial number is invalid
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new VectorAuthenticationException(VectorAuthenticationFailureType.SerialNumber, "Serial number is invalid.");
                }

                try
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    throw new VectorAuthenticationException(VectorAuthenticationFailureType.SerialNumber, ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Gets the session token for the user.
        /// </summary>
        /// <param name="emailAddress">The login email address.</param>
        /// <param name="password">The password.</param>
        /// <returns>A task that represents the asynchronous operation; the task result contains the session token.</returns>
        /// <exception cref="Anki.Vector.Exceptions.VectorAuthenticationException">
        /// Email must be provided.
        /// or
        /// Password must be provided.
        /// or
        /// Invalid email address or password.
        /// or
        /// Invalid response from Anki accounts API
        /// </exception>
        public static async Task<string> GetSessionToken(string emailAddress, string password)
        {
            if (string.IsNullOrWhiteSpace(emailAddress)) throw new ArgumentException("Email must be provided.", nameof(emailAddress));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password must be provided.", nameof(password));

            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + ".net";
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.api.anki.com/1/sessions"))
            {
                request.Headers.Add("Anki-App-Key", AnkiAppKey);
                request.Headers.UserAgent.ParseAdd("Vector-sdk/" + version);
                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", emailAddress),
                    new KeyValuePair<string, string>("password", password)
                });
                using (var response = await HttpClient.SendAsync(request))
                {
                    // If the result is forbidden then login is incorrect
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        throw new VectorAuthenticationException(VectorAuthenticationFailureType.Login, "Invalid email address or password.");
                    }

                    try
                    {
                        response.EnsureSuccessStatusCode();
                        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                        return json["session"]["session_token"].Value<string>();
                    }
                    catch (Exception ex)
                    {
                        throw new VectorAuthenticationException(VectorAuthenticationFailureType.Login, "Invalid response from Anki accounts API", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the authentication token from the robot.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="certificate">The SSL certificate for the robot.</param>
        /// <param name="robotName">Name of the robot.</param>
        /// <param name="ipAddress">The IP address of the robot.</param>
        /// <returns>A task that represents the asynchronous operation; the task result contains the authentication token.</returns>
        /// <exception cref="ArgumentException">
        /// Session ID must be provided - sessionId
        /// or
        /// SSL certificate must be provided - certificate
        /// </exception>
        /// <exception cref="Anki.Vector.Exceptions.VectorAuthenticationException">
        /// Robot name must be provided.
        /// or
        /// Robot name is not in the correct format.
        /// or
        /// IP address must be provided
        /// or
        /// Unable to establish a connection to Vector.
        /// or
        /// Failed to authorize request.  Please be sure to first set up Vector using the companion app.
        /// </exception>
        public static async Task<string> GetTokenGuid(string sessionId, string certificate, string robotName, IPAddress ipAddress)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentException("Session ID must be provided.", nameof(sessionId));
            if (string.IsNullOrEmpty(certificate)) throw new ArgumentException("SSL certificate must be provided.", nameof(certificate));
            if (string.IsNullOrEmpty(robotName)) throw new ArgumentException("Robot name must be provided.", nameof(robotName));
            if (!RobotNameIsValid(robotName)) throw new ArgumentException("Robot name is not in the correct format.", nameof(robotName));
            if (ipAddress == null) throw new ArgumentException("IP address must be provided.", nameof(ipAddress));

            // Create the channel
            var channel = new Channel(
                ipAddress.ToString() + ":443",
                ChannelCredentials.Create(new SslCredentials(certificate), CallCredentials.FromInterceptor((context, metadata) => Task.CompletedTask)),
                new ChannelOption[] { new ChannelOption("grpc.ssl_target_name_override", robotName) }
            );

            try
            {
                // Open the channel
                await channel.ConnectAsync(Robot.GrpcDeadline(15_000));
            }
            catch (Exception ex)
            {
                // If failed to open channel throw exception
                throw new VectorAuthenticationException(VectorAuthenticationFailureType.Connection, "Unable to establish a connection to Vector.", ex);
            }

            // Create the client and return the response
            var client = new ExternalInterfaceClient(channel);
            var response = await client.UserAuthenticationAsync(new UserAuthenticationRequest()
            {
                UserSessionId = Google.Protobuf.ByteString.CopyFromUtf8(sessionId),
                ClientName = Google.Protobuf.ByteString.CopyFromUtf8(Dns.GetHostName())
            });
            if (response.Code != UserAuthenticationResponse.Types.Code.Authorized)
            {
                throw new VectorAuthenticationException(VectorAuthenticationFailureType.Login, "Failed to authorize request.  Please be sure to first set up Vector using the companion app.");
            }
            await channel.ShutdownAsync();
            return response.ClientTokenGuid.ToStringUtf8();
        }

        /// <summary>
        /// Finds the robot IP address.
        /// </summary>
        /// <param name="robotName">Name of the robot.</param>
        /// <param name="timeout">The timeout in milliseconds.</param>
        /// <returns>A task that represents the asynchronous operation.  The task result contains the IP address of the robot (or null if not found).</returns>
        public static async Task<IPAddress> FindRobotAddress(string robotName, int timeout = Robot.DefaultConnectionTimeout)
        {
            if (string.IsNullOrEmpty(robotName)) throw new ArgumentException("Robot name must be provided.", nameof(robotName));
            if (!RobotNameIsValid(robotName)) throw new ArgumentException("Robot name is not in the correct format.", nameof(robotName));

            robotName = robotName.ToLower();
            var resultSource = new TaskCompletionSource<IPAddress>();

            // Run the resolver in the background to completion because stopping it early causes errors
            // But will set the result source and return the first matching IP address as soon as it's found
            // and complete this async method.
            _ = ZeroconfResolver.ResolveAsync(
                "_ankivector._tcp.local.",
                new TimeSpan(0, 0, 0, 0, timeout),
                callback: host =>
                {
                    if (host.DisplayName.ToLower() != robotName) return;
                    resultSource.TrySetResult(IPAddress.Parse(host.IPAddress));
                }
            ).ContinueWith((results) => resultSource.TrySetResult(null)).ConfigureAwait(false);

            return await resultSource.Task;
        }

        /// <summary>
        /// Standardizes the name of the robot.
        /// </summary>
        /// <param name="robotName">Name of the robot.</param>
        /// <returns>A robot name in the correct format or the name unchanged.</returns>
        /// <exception cref="System.ArgumentException">Invalid robot name. Please match the format exactly. Example: Vector-A1B2 - robotName</exception>
        public static string StandardizeRobotName(string robotName)
        {
            if (robotName == null) return robotName;
            robotName = robotName.ToUpper();
            if (robotName.Length == 4) robotName = "Vector-" + robotName;
            else robotName = robotName.Replace("VECTOR-", "Vector-");
            return robotName;
        }

        /// <summary>
        /// Validates the name of the robot.
        /// </summary>
        /// <param name="robotName">Name of the robot.</param>
        /// <returns>True if the robot name is in the correct format.</returns>
        public static bool RobotNameIsValid(string robotName)
        {
            if (robotName == null) return false;
            return Regex.IsMatch(robotName, @"\AVector-[A-Z0-9]{4}\Z");
        }

        /// <summary>
        /// Validates the serial number.
        /// </summary>
        /// <param name="serialNumber">The serial number.</param>
        /// <returns>True if the serial number is in the correct format.</returns>
        public static bool SerialNumberIsValid(string serialNumber)
        {
            if (serialNumber == null) return false;
            return Regex.IsMatch(serialNumber, @"\A[0-9a-f]{8}\Z");
        }

        /// <summary>
        /// Validates the specified robot configuration.
        /// </summary>
        /// <param name="robotConfiguration">The robot configuration.</param>
        /// <returns>A list of errors</returns>
        public static IEnumerable<string> TryValidate(this IRobotConfiguration robotConfiguration)
        {
            if (string.IsNullOrWhiteSpace(robotConfiguration.RobotName))
            {
                yield return "Robot name is missing";
            }
            if (string.IsNullOrWhiteSpace(robotConfiguration.SerialNumber))
            {
                yield return "Serial number is missing";
            }
            if (string.IsNullOrWhiteSpace(robotConfiguration.Certificate))
            {
                yield return "SSL certificate is missing";
            }
            if (string.IsNullOrWhiteSpace(robotConfiguration.Guid))
            {
                yield return "GUID token is missing";
            }
            if (!RobotNameIsValid(robotConfiguration.RobotName))
            {
                yield return "Invalid robot name. Please match the format exactly. Example: Vector-A1B2";
            }
            if (!SerialNumberIsValid(robotConfiguration.SerialNumber))
            {
                yield return "Serial number is not the correct format.";
            }
        }

        /// <summary>
        /// Validates the specified robot configuration.
        /// </summary>
        /// <param name="robotConfiguration">The robot configuration.</param>
        /// <returns>
        /// The robot configuration unchanged.
        /// </returns>
        /// <exception cref="VectorConfigurationException">Validation error</exception>
        public static IRobotConfiguration Validate(this IRobotConfiguration robotConfiguration)
        {
            var error = TryValidate(robotConfiguration).FirstOrDefault();
            if (string.IsNullOrEmpty(error)) return robotConfiguration;
            throw new VectorConfigurationException(error);
        }

        /// <summary>
        /// Finds the current robot address if possible or returns the address from the configuration.
        /// </summary>
        /// <param name="robotConfiguration">The robot configuration.</param>
        /// <returns>A task that represents the asynchronous operation; the task result contains the IP address.</returns>
        public static async Task<IPAddress> FindRobotAddress(this IRobotConfiguration robotConfiguration)
        {
            return await FindRobotAddress(robotConfiguration.RobotName) ?? robotConfiguration.IPAddress;
        }
    }
}
