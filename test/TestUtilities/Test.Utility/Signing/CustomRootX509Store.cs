// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Test.Utility.Signing
{
    internal sealed class CustomRootX509Store : IRootX509Store
    {
        internal static CustomRootX509Store Instance { get; } = new();

        public void Add(StoreLocation storeLocation, X509Certificate2 certificate)
        {
#if NET5_0_OR_GREATER
            X509Certificate2Collection certificates = TestFallbackCertificateBundleX509ChainFactory.Instance.Certificates;

            if (!certificates.Contains(certificate))
            {
                certificates.Add(certificate);
            }
#else
            throw new NotSupportedException();
#endif
        }

        public void Remove(StoreLocation storeLocation, X509Certificate2 certificate)
        {
#if NET5_0_OR_GREATER
            TestFallbackCertificateBundleX509ChainFactory.Instance.Certificates.Remove(certificate);
#else
            throw new NotSupportedException();
#endif
        }
    }
}
