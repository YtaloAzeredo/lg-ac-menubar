import Foundation
import Testing
@testable import LGACCore

@Suite struct ThinQRegionTests {
    @Test func brazilUsesAmericasServer() {
        #expect(ThinQRegion.region(forCountry: "BR") == .aic)
        #expect(ThinQRegion.baseURL(forCountry: "BR")?.absoluteString == "https://api-aic.lgthinq.com/")
    }

    @Test func otherRegions() {
        #expect(ThinQRegion.region(forCountry: "US") == .aic)
        #expect(ThinQRegion.region(forCountry: "DE") == .eic)
        #expect(ThinQRegion.region(forCountry: "KR") == .kic)
    }

    @Test func acceptsLowercase() {
        #expect(ThinQRegion.region(forCountry: "br") == .aic)
    }

    @Test func unknownCountry() {
        #expect(ThinQRegion.region(forCountry: "XX") == nil)
        #expect(ThinQRegion.baseURL(forCountry: "XX") == nil)
    }

    @Test func supportedCountriesAreSorted() {
        let countries = ThinQRegion.supportedCountries
        #expect(countries.contains("BR"))
        #expect(countries == countries.sorted())
    }
}
