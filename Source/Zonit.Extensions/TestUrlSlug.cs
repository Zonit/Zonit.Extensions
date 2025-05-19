//using Stand.Libraries.Core.Domain.ValueObjects;

//namespace Stand.Libraries.Core.Test.ValueObjects;

//public class UrlSlugTests
//{
//    [Theory]
//    [InlineData("Ala ma kota", "ala-ma-kota")]
//    [InlineData("Witaj, świecie!", "witaj-swiecie")]
//    [InlineData("Random 123 @!$%#", "random-123")]
//    [InlineData("    Lorem    ipsum    dolor   ", "lorem-ipsum-dolor")]
//    public void Constructor_ShouldConvertValueToValidUrlSlug(string input, string expectedOutput)
//    {
//        // Act
//        var urlSlug = new UrlSlug(input);

//        // Assert
//        Assert.Equal(expectedOutput, urlSlug.Value);
//    }

//    [Theory]
//    [InlineData("The quick brown fox jumps over the lazy dog", "the-quick-brown-fox-jumps-over-the-lazy-dog")]
//    [InlineData("   The  quick  brown  fox  jumps  over  the  lazy  dog  ", "the-quick-brown-fox-jumps-over-the-lazy-dog")]
//    [InlineData("THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG", "the-quick-brown-fox-jumps-over-the-lazy-dog")]
//    [InlineData("123   The  quick  brown  fox  jumps  over  the  lazy  dog  456", "123-the-quick-brown-fox-jumps-over-the-lazy-dog-456")]
//    [InlineData("Hello, World!", "hello-world")]
//    [InlineData("ąćęłńóśźż ĄĆĘŁŃÓŚŹŻ", "acelnoszz-acelnoszz")]
//    [InlineData("Good old-fashioned pancakes", "good-old-fashioned-pancakes")]
//    [InlineData("Good --- Morning", "good-morning")]
//    [InlineData("Emoji meanings 😊🚀", "emoji-meanings")]
//    [InlineData("Akcent - PRZEBOJE z lat 1999-2002 (Składanka piosenek)", "akcent-przeboje-z-lat-1999-2002-skladanka-piosenek")]
//    public void ToString_ReturnsValidUrlSlug(string input, string expected)
//    {
//        // Arrange
//        var urlSlug = new UrlSlug(input);

//        // Assert
//        Assert.Equal(expected, urlSlug.Value);
//    }

//    [Fact]
//    public void Equals_ShouldReturnTrue_WhenComparingSameInstance()
//    {
//        // Arrange
//        var urlSlug1 = new UrlSlug("test");
//        var urlSlug2 = urlSlug1;

//        // Act
//        var result = urlSlug1.Equals(urlSlug2);

//        // Assert
//        Assert.True(result);
//    }

//    [Fact]
//    public void Equals_ShouldReturnTrue_WhenComparingEqualInstances()
//    {
//        // Arrange
//        var urlSlug1 = new UrlSlug("test");
//        var urlSlug2 = new UrlSlug("test");

//        // Act
//        var result = urlSlug1.Equals(urlSlug2);

//        // Assert
//        Assert.True(result);
//    }

//    [Fact]
//    public void Equals_ShouldReturnFalse_WhenComparingDifferentInstances()
//    {
//        // Arrange
//        var urlSlug1 = new UrlSlug("test1");
//        var urlSlug2 = new UrlSlug("test2");

//        // Act
//        var result = urlSlug1.Equals(urlSlug2);

//        // Assert
//        Assert.False(result);
//    }

//    [Fact]
//    public void Equals_ShouldReturnFalse_WhenComparingWithNull()
//    {
//        // Arrange
//        var urlSlug1 = new UrlSlug("test");
//        UrlSlug urlSlug2 = null;

//        // Act
//        var result = urlSlug1.Equals(urlSlug2);

//        // Assert
//        Assert.False(result);
//    }

//    [Fact]
//    public void GetHashCode_ShouldReturnSameHashCode_WhenCalledOnEqualInstances()
//    {
//        // Arrange
//        var urlSlug1 = new UrlSlug("test");
//        var urlSlug2 = new UrlSlug("test");

//        // Act
//        var hash1 = urlSlug1.GetHashCode();
//        var hash2 = urlSlug2.GetHashCode();

//        // Assert
//        Assert.Equal(hash1, hash2);
//    }

//    [Fact]
//    public void GetHashCode_ShouldReturnDifferentHashCode_WhenCalledOnDifferentInstances()
//    {
//        // Arrange
//        var urlSlug1 = new UrlSlug("test1");
//        var urlSlug2 = new UrlSlug("test2");

//        // Act
//        var hash1 = urlSlug1.GetHashCode();
//        var hash2 = urlSlug2.GetHashCode();

//        // Assert
//        Assert.NotEqual(hash1, hash2);
//    }

//    [Fact]
//    public void ToString_ShouldReturnUrlSlugValue()
//    {
//        // Arrange
//        var urlSlug = new UrlSlug("test");

//        // Act
//        var result = urlSlug.ToString();

//        // Assert
//        Assert.Equal(urlSlug.Value, result);
//    }

//    [Fact]
//    public void ToString_ReturnsUniqueUrlSlug()
//    {
//        // Arrange
//        var existingUrls = new List<string> { "ala-ma-kota", "ala-ma-kota-1" };
//        var urlSlug = new UrlSlug("Ala ma kota", (url) => {
//            return existingUrls.Where(x => x == url).ToList();
//        });

//        // Act
//        string result = urlSlug.ToString();

//        // Assert
//        Assert.NotEmpty(result);
//        Assert.DoesNotContain("ala-ma-kota-2", existingUrls);
//    }
//}