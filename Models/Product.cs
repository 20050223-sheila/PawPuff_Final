using System.ComponentModel.DataAnnotations;
namespace PawPuff.Models;
public class Product {
 public int Id {get;set;}
 [Required, StringLength(100)] public string Name {get;set;} = "";
 [Required, StringLength(100)] public string Category {get;set;} = "Food";
 [Required, StringLength(20)] public string PetType {get;set;} = "Dog";
 [StringLength(1000)] public string Description {get;set;} = "";
 [Range(0.01, 100000)] public decimal Price {get;set;}
 [Range(0, 100000)] public int Stock {get;set;}
 public string ImageUrl {get;set;} = "https://placehold.co/600x400?text=Pet+Product";
 public bool IsActive {get;set;} = true;
}
