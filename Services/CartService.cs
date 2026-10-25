using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PawPuff.Models;
namespace PawPuff.Services;
public class CartService(IHttpContextAccessor accessor) {
 private const string Key="PAWSHOP_CART";
 private ISession Session => accessor.HttpContext!.Session;
 public List<CartLine> Get() => JsonSerializer.Deserialize<List<CartLine>>(Session.GetString(Key) ?? "[]") ?? new();
 public void Save(List<CartLine> lines) => Session.SetString(Key,JsonSerializer.Serialize(lines));
 public void Clear()=>Session.Remove(Key);
}
