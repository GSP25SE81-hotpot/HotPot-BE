﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace Capstone.HPTY.ModelLayer.Entities
{
    public class Combo : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ComboId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Size { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BasePrice  { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(2000)]
        public string? ImageURL { get; set; }

        [NotMapped]
        public string[]? ImageURLs
        {
            get
            {
                if (string.IsNullOrEmpty(ImageURL))
                    return null;

                try
                {
                    return JsonSerializer.Deserialize<string[]>(ImageURL);
                }
                catch (JsonException)
                {
                    // Log the error if you have a logger
                    // _logger.LogWarning($"Failed to deserialize ImageURL: {ImageURL}");

                    // If it looks like a single URL rather than a JSON array, return it as a single-element array
                    if (ImageURL.StartsWith("h") && (ImageURL.Contains("http://") || ImageURL.Contains("https://")))
                    {
                        return new[] { ImageURL };
                    }

                    // Return an empty array as fallback
                    return Array.Empty<string>();
                }
            }
            set => ImageURL = value != null ? JsonSerializer.Serialize(value) : null;
        }

        [Required]
        public bool IsCustomizable { get; set; } = false;

        [ForeignKey("AppliedDiscount")]
        public int? AppliedDiscountId { get; set; }

        [Required]
        [ForeignKey("TurtorialVideo")]
        public int TurtorialVideoId { get; set; }

        public virtual TurtorialVideo? TurtorialVideo { get; set; }
        public virtual ICollection<Customization>? Customizations { get; set; }
        public virtual ICollection<SellOrderDetail>? SellOrderDetails { get; set; }
        public virtual ICollection<ComboIngredient>? ComboIngredients { get; set; } = new List<ComboIngredient>();
        public virtual ICollection<ComboAllowedIngredientType>? AllowedIngredientTypes { get; set; } = new List<ComboAllowedIngredientType>();
        public virtual SizeDiscount? AppliedDiscount { get; set; }

    }
}
