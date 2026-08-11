using System.Security.Cryptography;
namespace ReimbursementAssistant.Services;
public sealed class HashService { public string Compute(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); } }
