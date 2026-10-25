namespace PawPuff.Models;
public class CartLine { public int ProductId {get;set;} public string Name {get;set;} = ""; public decimal Price {get;set;} public int Quantity {get;set;} public int Stock {get;set;} public string ImageUrl {get;set;} = ""; public decimal Subtotal => Price * Quantity; }
