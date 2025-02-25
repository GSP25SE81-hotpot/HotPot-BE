﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public decimal TotalPrice { get; set; }

        [Range(0, 100)]
        public double Discount { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }  

        [ForeignKey("HotpotBroth")]
        public int HotpotBrothID { get; set; }

        public virtual Ingredient? HotpotBroth { get; set; }
        public virtual Customization? Customization { get; set; }
        public virtual OrderDetail? OrderDetail { get; set; }
        public virtual ICollection<ComboIngredient>? ComboIngredients { get; set; } = new List<ComboIngredient>();

    }
}
