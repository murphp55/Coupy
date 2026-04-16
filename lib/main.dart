import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

const _apiBaseUrl = String.fromEnvironment(
  'WALGREENS_API_BASE_URL',
  defaultValue: 'http://localhost:5075',
);

void main() {
  runApp(const WalgreensOffersApp());
}

class WalgreensOffersApp extends StatelessWidget {
  const WalgreensOffersApp({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = ThemeData(
      useMaterial3: true,
      colorScheme: ColorScheme.fromSeed(
        seedColor: const Color(0xFF1C6A70),
        brightness: Brightness.light,
      ),
    );

    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'Walgreens Digital Offers',
      theme: theme,
      home: const OffersPage(),
    );
  }
}

class OffersPage extends StatefulWidget {
  const OffersPage({super.key});

  @override
  State<OffersPage> createState() => _OffersPageState();
}

class _OffersPageState extends State<OffersPage> {
  final TextEditingController _phoneController = TextEditingController();
  Future<OffersResponse>? _offersFuture;
  String? _encLoyaltyId;
  String? _lookupError;
  bool _isLookingUp = false;

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _lookupLoyalty() async {
    final phoneNumber = _phoneController.text.trim();
    if (phoneNumber.isEmpty) {
      setState(() {
        _lookupError = 'Enter a phone number to look up.';
      });
      return;
    }

    setState(() {
      _isLookingUp = true;
      _lookupError = null;
    });

    try {
      final uri = Uri.parse('$_apiBaseUrl/api/walgreens/loyalty-lookup');
      final response = await http.post(
        uri,
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'phoneNumber': phoneNumber}),
      );

      if (response.statusCode != 200) {
        setState(() {
          _lookupError = 'Lookup failed (${response.statusCode}).';
        });
        return;
      }

      final payload = jsonDecode(response.body) as Map<String, dynamic>;
      final matchProfiles =
          (payload['matchProfiles'] as List<dynamic>? ?? <dynamic>[])
              .cast<Map<String, dynamic>>();
      final loyaltyId = matchProfiles.isNotEmpty
          ? matchProfiles.first['loyaltyMemberId'] as String?
          : null;

      if (loyaltyId == null || loyaltyId.trim().isEmpty) {
        setState(() {
          _lookupError = 'No loyalty member found for that phone number.';
        });
        return;
      }

      setState(() {
        _encLoyaltyId = loyaltyId;
        _offersFuture = _fetchOffers(loyaltyId);
      });
    } catch (error) {
      setState(() {
        _lookupError = 'Lookup failed: $error';
      });
    } finally {
      if (mounted) {
        setState(() {
          _isLookingUp = false;
        });
      }
    }
  }

  Future<OffersResponse> _fetchOffers(String loyaltyId) async {
    final uri = Uri.parse('$_apiBaseUrl/api/walgreens/offers')
        .replace(queryParameters: {'encLoyaltyId': loyaltyId});
    final response = await http.get(uri);

    if (response.statusCode != 200) {
      throw Exception('Failed to load offers (${response.statusCode}).');
    }

    final payload = jsonDecode(response.body) as Map<String, dynamic>;
    final offersJson = (payload['offers'] as List<dynamic>? ?? <dynamic>[])
        .cast<Map<String, dynamic>>();
    final offers = offersJson
        .map((offer) => WalgreensOffer.fromJson(offer))
        .toList();
    final total = (payload['total'] as num?)?.toInt() ?? offers.length;

    return OffersResponse(offers: offers, total: total);
  }

  void _reload() {
    if (_encLoyaltyId == null) {
      return;
    }
    setState(() {
      _offersFuture = _fetchOffers(_encLoyaltyId!);
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Walgreens Digital Offers'),
        actions: [
          IconButton(
            onPressed: _encLoyaltyId == null ? null : _reload,
            icon: const Icon(Icons.refresh),
            tooltip: 'Reload offers',
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _LookupCard(
            controller: _phoneController,
            onLookup: _isLookingUp ? null : _lookupLoyalty,
            error: _lookupError,
            isLoading: _isLookingUp,
            hasLoyalty: _encLoyaltyId != null,
          ),
          const SizedBox(height: 12),
          if (_offersFuture == null)
            const _EmptyLookupState()
          else
            FutureBuilder<OffersResponse>(
              future: _offersFuture,
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting) {
                  return const Center(child: CircularProgressIndicator());
                }

                if (snapshot.hasError) {
                  return _ErrorState(
                    message: snapshot.error.toString(),
                    onRetry: _reload,
                  );
                }

                final response = snapshot.data;
                if (response == null || response.offers.isEmpty) {
                  return _EmptyState(onReload: _reload);
                }

                return Column(
                  children: [
                    _SummaryCard(total: response.total),
                    const SizedBox(height: 12),
                    ...response.offers.map((offer) => _OfferCard(offer: offer)),
                  ],
                );
              },
            ),
        ],
      ),
    );
  }
}

class _LookupCard extends StatelessWidget {
  const _LookupCard({
    required this.controller,
    required this.onLookup,
    required this.error,
    required this.isLoading,
    required this.hasLoyalty,
  });

  final TextEditingController controller;
  final VoidCallback? onLookup;
  final String? error;
  final bool isLoading;
  final bool hasLoyalty;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Loyalty lookup',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            const Text('Enter a phone number to load Walgreens digital offers.'),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: controller,
                    keyboardType: TextInputType.phone,
                    decoration: const InputDecoration(
                      hintText: 'Phone number',
                      border: OutlineInputBorder(),
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                FilledButton(
                  onPressed: onLookup,
                  child: isLoading
                      ? const SizedBox(
                          height: 18,
                          width: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(hasLoyalty ? 'Reload' : 'Lookup'),
                ),
              ],
            ),
            if (error != null) ...[
              const SizedBox(height: 12),
              Text(
                error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.total});

  final int total;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            const Icon(Icons.local_offer, size: 32),
            const SizedBox(width: 12),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '$total offers loaded',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 4),
                const Text('Results from Walgreens Digital Offers API'),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _OfferCard extends StatelessWidget {
  const _OfferCard({required this.offer});

  final WalgreensOffer offer;

  @override
  Widget build(BuildContext context) {
    final title = offer.brandName?.trim().isNotEmpty == true
        ? offer.brandName!
        : (offer.summary?.trim().isNotEmpty == true
            ? offer.summary!
            : 'Walgreens Offer');
    final description = offer.description ?? 'No description provided.';

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 6),
            Text(description),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                if (offer.categoryName != null)
                  _InfoChip(label: offer.categoryName!),
                if (offer.expiryDate != null)
                  _InfoChip(label: 'Expires ${offer.expiryDate}'),
                if (offer.offerValue != null)
                  _InfoChip(label: '\$${offer.offerValue} value'),
                if (offer.minQty != null)
                  _InfoChip(label: 'Min qty ${offer.minQty}'),
              ],
            ),
            if (offer.image != null || offer.image2 != null) ...[
              const SizedBox(height: 12),
              _OfferImage(url: offer.image2 ?? offer.image!),
            ],
          ],
        ),
      ),
    );
  }
}

class _OfferImage extends StatelessWidget {
  const _OfferImage({required this.url});

  final String url;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(12),
      child: Image.network(
        url,
        height: 180,
        width: double.infinity,
        fit: BoxFit.cover,
        errorBuilder: (_, __, ___) => Container(
          height: 180,
          color: const Color(0xFFEFEFEF),
          alignment: Alignment.center,
          child: const Icon(Icons.broken_image_outlined),
        ),
      ),
    );
  }
}

class _InfoChip extends StatelessWidget {
  const _InfoChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: const Color(0xFFE7EFF0),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(label),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.onReload});

  final VoidCallback onReload;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.local_offer_outlined, size: 48),
          const SizedBox(height: 12),
          const Text('No offers returned.'),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: onReload,
            icon: const Icon(Icons.refresh),
            label: const Text('Try again'),
          ),
        ],
      ),
    );
  }
}

class _EmptyLookupState extends StatelessWidget {
  const _EmptyLookupState();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.only(top: 24),
      child: Center(
        child: Text('Enter a phone number to fetch offers.'),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 48),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Retry'),
            ),
          ],
        ),
      ),
    );
  }
}

class OffersResponse {
  const OffersResponse({required this.offers, required this.total});

  final List<WalgreensOffer> offers;
  final int total;
}

class WalgreensOffer {
  const WalgreensOffer({
    this.id,
    this.description,
    this.summary,
    this.brandName,
    this.categoryName,
    this.image,
    this.image2,
    this.offerValue,
    this.minQty,
    this.expiryDate,
    this.activeDate,
  });

  final String? id;
  final String? description;
  final String? summary;
  final String? brandName;
  final String? categoryName;
  final String? image;
  final String? image2;
  final num? offerValue;
  final int? minQty;
  final String? expiryDate;
  final String? activeDate;

  factory WalgreensOffer.fromJson(Map<String, dynamic> json) {
    return WalgreensOffer(
      id: json['id'] as String?,
      description: json['description'] as String?,
      summary: json['summary'] as String?,
      brandName: json['brandName'] as String?,
      categoryName: json['categoryName'] as String?,
      image: json['image'] as String?,
      image2: json['image2'] as String?,
      offerValue: json['offerValue'] as num?,
      minQty: (json['minQty'] as num?)?.toInt(),
      expiryDate: json['expiryDate'] as String?,
      activeDate: json['activeDate'] as String?,
    );
  }
}
