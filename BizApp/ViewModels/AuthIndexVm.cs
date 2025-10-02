using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BizApp.Models;

namespace BizApp.ViewModels;

public class AuthIndexVm
{
    // Profile basics
    public long CustomerId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    // Phone update (we will hash it; we cannot show the real phone)
    [Phone]
    public string? NewPhone { get; set; }

    // Cards
    public List<CardRow> Cards { get; set; } = new();

    // New card form
    public NewCardVm NewCard { get; set; } = new();

    public class CardRow
    {
        public long CardId { get; set; }
        public string Network { get; set; } = "";
        public string Last4 { get; set; } = "";
        public string IssueCountry { get; set; } = "";
    }

    public class NewCardVm
    {
        [Required]
        public string Network { get; set; } = "Visa";

        [Required, StringLength(4, MinimumLength = 4)]
        public string Last4 { get; set; } = "";

        [Required, StringLength(2, MinimumLength = 2)]
        public string IssueCountry { get; set; } = "ZA";
    }
}
