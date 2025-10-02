using System.ComponentModel.DataAnnotations;

namespace BizApp.ViewModels;

public class RegisterVm
{
    [Required, StringLength(200)]
    public string Name { get; set; } = null!;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = null!;

    [Phone]
    public string? Phone { get; set; }

    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = null!;

    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = null!;
}

public class LoginVm
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    public bool RememberMe { get; set; } = true;
}
