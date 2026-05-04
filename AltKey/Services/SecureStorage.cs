using System.Security.Cryptography;
using System.Text;

namespace AltKey.Services;

/// <summary>
/// [역할] Windows DPAPI(Data Protection API)를 사용하여 민감한 문자열(API 키 등)을 안전하게 암호화/복호화하는 유틸리티입니다.
/// [기능] 현재 윈도우 사용자만 복호화할 수 있으므로, config.json에 평문 대신 암호화된 문자열을 저장합니다.
/// [참고] Windows 전용 기능입니다. DataProtectionScope.CurrentUser를 사용합니다.
/// </summary>
public static class SecureStorage
{
    // 엔트로피: 같은 데이터라도 앱마다 다른 결과가 나오게 하는 추가 보호 값입니다.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AltKey.SecureStorage");

    /// <summary>
    /// 평문 문자열을 DPAPI로 암호화하여 Base64 문자열로 반환합니다.
    /// </summary>
    /// <param name="plainText">암호화할 원본 문자열</param>
    /// <returns>암호화된 Base64 문자열</returns>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encrypted = ProtectedData.Protect(
            plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// DPAPI로 암호화된 Base64 문자열을 복호화하여 원본 평문을 반환합니다.
    /// </summary>
    /// <param name="base64Encrypted">암호화된 Base64 문자열</param>
    /// <returns>복호화된 평문 문자열, 실패 시 빈 문자열</returns>
    public static string Decrypt(string base64Encrypted)
    {
        if (string.IsNullOrEmpty(base64Encrypted)) return "";

        try
        {
            byte[] encrypted = Convert.FromBase64String(base64Encrypted);
            byte[] decrypted = ProtectedData.Unprotect(
                encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            // 다른 사용자 계정에서 암호화된 데이터거나 데이터가 손상된 경우
            return "";
        }
        catch (FormatException)
        {
            // Base64 형식이 아닌 경우 (예전 설정에 평문이 남아있을 때)
            return "";
        }
    }
}
