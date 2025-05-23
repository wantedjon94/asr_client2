﻿/*
    Copyright 2020-2023 PicovoiceApp Inc.

    You may not use this file except in compliance with the license. A copy of the license is located in the "LICENSE"
    file accompanying this source.

    Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
    an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
    specific language governing permissions and limitations under the License.
*/

using System;

namespace Pv
{
    public class PicovoiceException : Exception
    {
        public PicovoiceException() { }

        public PicovoiceException(string message) : base(message) { }

        public PicovoiceException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceMemoryException : PicovoiceException
    {
        public PicovoiceMemoryException() { }

        public PicovoiceMemoryException(string message) : base(message) { }

        public PicovoiceMemoryException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceIOException : PicovoiceException
    {
        public PicovoiceIOException() { }

        public PicovoiceIOException(string message) : base(message) { }

        public PicovoiceIOException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceInvalidArgumentException : PicovoiceException
    {
        public PicovoiceInvalidArgumentException() { }

        public PicovoiceInvalidArgumentException(string message) : base(message) { }

        public PicovoiceInvalidArgumentException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceStopIterationException : PicovoiceException
    {
        public PicovoiceStopIterationException() { }

        public PicovoiceStopIterationException(string message) : base(message) { }

        public PicovoiceStopIterationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceKeyException : PicovoiceException
    {
        public PicovoiceKeyException() { }

        public PicovoiceKeyException(string message) : base(message) { }

        public PicovoiceKeyException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceInvalidStateException : PicovoiceException
    {
        public PicovoiceInvalidStateException() { }

        public PicovoiceInvalidStateException(string message) : base(message) { }

        public PicovoiceInvalidStateException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceRuntimeException : PicovoiceException
    {
        public PicovoiceRuntimeException() { }

        public PicovoiceRuntimeException(string message) : base(message) { }

        public PicovoiceRuntimeException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceActivationException : PicovoiceException
    {
        public PicovoiceActivationException() { }

        public PicovoiceActivationException(string message) : base(message) { }

        public PicovoiceActivationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceActivationLimitException : PicovoiceException
    {
        public PicovoiceActivationLimitException() { }

        public PicovoiceActivationLimitException(string message) : base(message) { }

        public PicovoiceActivationLimitException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceActivationThrottledException : PicovoiceException
    {
        public PicovoiceActivationThrottledException() { }

        public PicovoiceActivationThrottledException(string message) : base(message) { }

        public PicovoiceActivationThrottledException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PicovoiceActivationRefusedException : PicovoiceException
    {
        public PicovoiceActivationRefusedException() { }

        public PicovoiceActivationRefusedException(string message) : base(message) { }

        public PicovoiceActivationRefusedException(string message, Exception innerException) : base(message, innerException) { }
    }
}