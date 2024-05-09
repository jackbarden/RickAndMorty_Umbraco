using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis;
using Umbraco.Cms.Core.Models;
using Lucene.Net.Util;
using Umbraco.Cms.Core;
using Constants = Umbraco.Cms.Core.Constants;

namespace RickAndMorty_Umbraco.Controllers
{
	[Route("api/Character")]
	[ApiController]
	public class CharacterController : UmbracoApiController
	{
		private readonly IContentService _contentService;
		private readonly IContentTypeService _contentTypeService;
		private readonly IMediaService _mediaService;
		private readonly IHttpClientFactory _httpClientFactory;

		public CharacterController(IContentService contentService, IMediaService mediaService, IHttpClientFactory httpClientFactory, IContentTypeService contentTypeService)
		{
			_contentService = contentService;
			_mediaService = mediaService;
			_httpClientFactory = httpClientFactory;
			_contentTypeService = contentTypeService;
		}

		[HttpGet]
		[Route("PopulateCharacters")]
		public async Task<ResponseObject<bool>> PopulateCharacters(int totalRecords)
		{
			var rspObj = new ResponseObject<bool> { Data = false, Message = "", Success = false };

			var httpClient = _httpClientFactory.CreateClient();
			var characterUrl = "https://rickandmortyapi.com/api/character";
			var characters = new List<Result>(); // List to collect all characters

			do
			{
				var response = await httpClient.GetAsync(characterUrl);

				if (!response.IsSuccessStatusCode)
				{
					rspObj.Message = "Failed to retrieve characters from Rick and Morty API";
					return rspObj;
				}

				var contentString = await response.Content.ReadAsStringAsync();
				var characterResponse = JsonConvert.DeserializeObject<CharacterResponse>(contentString);

				characters.AddRange(characterResponse.Results); // Add characters from current page 

				characterUrl = characterResponse?.Info?.Next; // Update URL for next page (if available)

			} while (!string.IsNullOrWhiteSpace(characterUrl));

			var rootFolder = _contentService.GetById(1061);
			var homeFolder = _contentService.GetById(1062);
			var allChildren = _contentService.GetPagedChildren(1062, 0, int.MaxValue, out long total);
			var charactersFolder = allChildren.FirstOrDefault(x => x.Name == "Characters");

			if (charactersFolder == null)
			{
				charactersFolder = _contentService.CreateContent("Characters", homeFolder!.GetUdi(), "contentFolder");
				_contentService.SaveAndPublish(charactersFolder);
			}

			var existingCharacters = _contentService.GetPagedChildren(charactersFolder.Id, 0, int.MaxValue, out long total2);

			foreach (var character in characters)
			{
				// Check if character with the same name already exists
				var existingCharacter = existingCharacters.FirstOrDefault(x => x.Name == $"{character.Name} ({character.Id})");
				if (existingCharacter == null)
				{
					var newCharacter = _contentService.CreateContent($"{character.Name} ({character.Id})", charactersFolder.GetUdi(), "character");
					newCharacter.SetValue("characterName", character.Name);
					newCharacter.SetValue("characterStatus", character.Status);
					newCharacter.SetValue("characterSpecies", character.Species);
					newCharacter.SetValue("characterGender", character.Gender);

					var image = await ProcessCharacterData(character);
					newCharacter.SetValue("characterImage", image);

					_contentService.SaveAndPublish(newCharacter);
				}
				else
				{
					Console.WriteLine($"Skipping duplicate character: {character.Name}");
				}
			}

			rspObj.Success = true;
			rspObj.Data = true;
			rspObj.Message = "Characters populated successfully!";
			return rspObj;
		}

		[HttpGet]
		[Route("HalveRandomCharacters")]
		public async Task<ResponseObject<bool>> HalveRandomCharacters()
		{
			var rspObj = new ResponseObject<bool> { Data = false, Message = "", Success = false };

			try
			{
				// Get all characters
				var characters = GetCharacterNodes();

				if (characters.Count() <= 1)
				{
					rspObj.Message = "There are not enough characters to halve.";
					return rspObj;
				}

				// Randomly select half the characters to delete
				var charactersToDelete = characters.OrderBy(x => Guid.NewGuid()).Take(characters.Count() / 2).ToList();

				// Delete the characters
				foreach (var character in charactersToDelete)
				{
					_contentService.Delete(character);
				}

				rspObj.Success = true;
				rspObj.Data = true;
				rspObj.Message = "Successfully halved the population - the balance has been restored.";
			}
			catch (Exception ex)
			{
				rspObj.Message = $"An error occurred halving characters: {ex.Message}";
			}

			return rspObj;
		}

		[HttpGet]
		[Route("DeleteAllCharacters")]
		public async Task<ResponseObject<bool>> DeleteAllCharacters()
		{
			var rspObj = new ResponseObject<bool> { Data = false, Message = "", Success = false };

			try
			{
				// Get all characters
				var characters = GetCharacterNodes();

				// Delete the characters
				foreach (var character in characters)
				{
					_contentService.Delete(character);
				}

				rspObj.Success = true;
				rspObj.Data = true;
				rspObj.Message = "Successfully destroyed the population.";
			}
			catch (Exception ex)
			{
				rspObj.Message = $"An error occurred deleting characters: {ex.Message}";
			}

			return rspObj;
		}

		[HttpGet]
		[Route("Count")]
		public int GetCharacterCount()
		{
			var characters = GetCharacterNodes();
			if (characters != null && characters.Any()) return characters.Count;
			return 0;
		}

		// Helper method to get all character nodes
		private List<IContent>? GetCharacterNodes()
		{
			var rootFolder = _contentService.GetById(1061);
			var homeFolder = _contentService.GetById(1062);
			var allChildren = _contentService.GetPagedChildren(1062, 0, int.MaxValue, out long total);
			var charactersFolder = allChildren.FirstOrDefault(x => x.Name == "Characters");

			if (charactersFolder == null)
			{
				// throw new Exception("Characters folder not found.");
				return null;
			}

			var characters = _contentService.GetPagedChildren(charactersFolder.Id, 0, int.MaxValue, out long total2);
			return characters.ToList();
		}

		public async Task<byte[]> DownloadImage(string imageUrl)
		{
			using (var httpClient = new HttpClient())
			{
				using (var response = await httpClient.GetAsync(imageUrl))
				{
					response.EnsureSuccessStatusCode();
					return await response.Content.ReadAsByteArrayAsync();
				}
			}
		}

		public async Task<IMedia> CreateMediaItem(string imageName, byte[] imageData, int folderId)
		{
			var media = _mediaService.CreateMedia(imageName, folderId, Constants.Conventions.MediaTypes.Image);

			using (var imageStream = new MemoryStream(imageData))
			{
				media.SetValue("umbracoFile", imageStream);
			}

			_mediaService.Save(media);
			return media;
		}

		async Task<int> ProcessCharacterData(Result data)
		{
			var downloadedImage = await DownloadImage(data.Image);
			var mediaItem = await CreateMediaItem(data.Name + ".jpg", downloadedImage, 4403);
			return mediaItem.Id;
		}
	}

	public class CharacterResponse
	{
		public Info? Info { get; set; }
		public List<Result>? Results { get; set; }
	}

	public class Info
	{
		public int Count { get; set; }
		public int Pages { get; set; }
		public string? Next { get; set; }
		public string? Prev { get; set; }
	}

	public class NameUrl
	{
		public string? Name { get; set; }
		public string? Url { get; set; }
	}

	public class Result
	{
		public int Id { get; set; }
		public string? Name { get; set; }
		public string? Status { get; set; }
		public string? Species { get; set; }
		public string? Type { get; set; }
		public string? Gender { get; set; }
		public NameUrl? Origin { get; set; }
		public NameUrl? Location { get; set; }
		public string? Image { get; set; }
		public List<string>? Episode { get; set; }
		public string? Url { get; set; }
		public DateTime Created { get; set; }
	}

	public class ResponseObject<T>
	{
		public required T Data { get; set; }
		public bool Success { get; set; }
		public string? Message { get; set; }
	}

}
